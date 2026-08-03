using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.Json;
using IFS.AIWeb.Application;
using IFS.AIWeb.Domain;
using Microsoft.EntityFrameworkCore;

namespace IFS.AIWeb.Infrastructure;

public sealed class GroqOptions
{
    public string BaseUrl { get; set; } = "https://api.groq.com/openai/v1/";
    public string Model { get; set; } = "openai/gpt-oss-120b";
    public string ApiKey { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 30;
    public int MaxOutputTokens { get; set; } = 500;
}

internal sealed class SummaryRepository(AuthDbContext db) : ISummaryRepository
{
    public void Add(SummaryRecord record) => db.SummaryRecords.Add(record);
    public async Task<IReadOnlyList<SummaryRecord>> GetRecentSuccessfulAsync(Guid userId, int limit, DateTimeOffset now, CancellationToken ct) =>
        await db.SummaryRecords.AsNoTracking().Where(x => x.UserId == userId && x.Status == SummaryStatus.Succeeded && x.ExpiresAtUtc > now)
            .OrderByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.Id).Take(limit).ToListAsync(ct);
    public Task<SummaryRecord?> GetSuccessfulDetailAsync(Guid id, Guid userId, DateTimeOffset now, CancellationToken ct) =>
        db.SummaryRecords.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.UserId == userId &&
            x.Status == SummaryStatus.Succeeded && x.ExpiresAtUtc > now, ct);
}

public sealed class GroqSummarizer(HttpClient client, GroqOptions options) : ILlmSummarizer
{
    public async Task<LlmSummary> SummarizeAsync(PromptEnvelope prompt, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
        request.Content = JsonContent.Create(new GroqRequest(options.Model,
            [new("system", prompt.SystemInstruction), new("user", prompt.UserContent)], options.MaxOutputTokens, 0,
            new("json_schema", new("summary_result", true, new("object", false,
                new Dictionary<string, JsonSchemaProperty> { ["quality"] = new("string", ["sufficient", "insufficient"]), ["summary"] = new("string", null) },
                ["quality", "summary"])))));
        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode) throw Failure(response.StatusCode switch
            { HttpStatusCode.TooManyRequests => LlmFailureKind.RateLimited, HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => LlmFailureKind.Configuration, _ when (int)response.StatusCode >= 500 => LlmFailureKind.Unavailable, _ => LlmFailureKind.InvalidResponse });
            var body = await response.Content.ReadFromJsonAsync<GroqResponse>(cancellationToken: ct);
            var content = body?.Choices?.FirstOrDefault()?.Message?.Content;
            if (string.IsNullOrWhiteSpace(content)) throw Failure(LlmFailureKind.InvalidResponse);
            StructuredSummary? structured;
            try { structured = JsonSerializer.Deserialize<StructuredSummary>(content); }
            catch (JsonException) { throw Failure(LlmFailureKind.InvalidResponse); }
            if (structured?.Additional is { Count: > 0 }) throw Failure(LlmFailureKind.InvalidResponse);
            var quality = structured?.Quality switch
            {
                "sufficient" when !string.IsNullOrWhiteSpace(structured.Summary) => SummaryContentQuality.Sufficient,
                "insufficient" when string.IsNullOrEmpty(structured.Summary) => SummaryContentQuality.Insufficient,
                _ => throw Failure(LlmFailureKind.InvalidResponse)
            };
            return new(structured!.Summary, quality, "Groq", options.Model);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw Failure(LlmFailureKind.Timeout); }
        catch (HttpRequestException) { throw Failure(LlmFailureKind.Unavailable); }
    }
    private LlmProviderException Failure(LlmFailureKind kind) => new(kind, "Groq", options.Model);
    private sealed record GroqRequest([property: JsonPropertyName("model")] string Model, [property: JsonPropertyName("messages")] GroqMessage[] Messages,
        [property: JsonPropertyName("max_completion_tokens")] int MaxTokens, [property: JsonPropertyName("temperature")] double Temperature,
        [property: JsonPropertyName("response_format")] ResponseFormat ResponseFormat);
    private sealed record GroqMessage([property: JsonPropertyName("role")] string Role, [property: JsonPropertyName("content")] string Content);
    private sealed record GroqResponse([property: JsonPropertyName("choices")] GroqChoice[]? Choices);
    private sealed record GroqChoice([property: JsonPropertyName("message")] GroqResponseMessage? Message);
    private sealed record GroqResponseMessage([property: JsonPropertyName("content")] string? Content);
    private sealed record ResponseFormat([property: JsonPropertyName("type")] string Type, [property: JsonPropertyName("json_schema")] JsonSchema Schema);
    private sealed record JsonSchema([property: JsonPropertyName("name")] string Name, [property: JsonPropertyName("strict")] bool Strict,
        [property: JsonPropertyName("schema")] JsonSchemaDefinition Definition);
    private sealed record JsonSchemaDefinition([property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("additionalProperties")] bool AdditionalProperties,
        [property: JsonPropertyName("properties")] Dictionary<string, JsonSchemaProperty> Properties,
        [property: JsonPropertyName("required")] string[] Required);
    private sealed record JsonSchemaProperty([property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("enum"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string[]? Enum);
    private sealed class StructuredSummary
    {
        [JsonPropertyName("quality")] public string? Quality { get; init; }
        [JsonPropertyName("summary")] public string? Summary { get; init; }
        [JsonExtensionData] public Dictionary<string, JsonElement>? Additional { get; init; }
    }
}

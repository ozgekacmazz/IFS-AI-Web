using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
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
    public async Task<IReadOnlyList<SummaryRecord>> GetRecentSuccessfulAsync(Guid userId, int limit, CancellationToken ct) =>
        await db.SummaryRecords.AsNoTracking().Where(x => x.UserId == userId && x.Status == SummaryStatus.Succeeded)
            .OrderByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.Id).Take(Math.Min(limit, 3)).ToListAsync(ct);
}

public sealed class GroqSummarizer(HttpClient client, GroqOptions options) : ILlmSummarizer
{
    public async Task<LlmSummary> SummarizeAsync(PromptEnvelope prompt, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
        request.Content = JsonContent.Create(new GroqRequest(options.Model,
            [new("system", prompt.SystemInstruction), new("user", prompt.UserContent)], options.MaxOutputTokens, 0.2));
        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode) throw Failure(response.StatusCode switch
            { HttpStatusCode.TooManyRequests => LlmFailureKind.RateLimited, HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => LlmFailureKind.Configuration, _ when (int)response.StatusCode >= 500 => LlmFailureKind.Unavailable, _ => LlmFailureKind.InvalidResponse });
            var body = await response.Content.ReadFromJsonAsync<GroqResponse>(cancellationToken: ct);
            var text = body?.Choices?.FirstOrDefault()?.Message?.Content;
            if (string.IsNullOrWhiteSpace(text)) throw Failure(LlmFailureKind.InvalidResponse);
            return new(text, "Groq", options.Model);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw Failure(LlmFailureKind.Timeout); }
        catch (HttpRequestException) { throw Failure(LlmFailureKind.Unavailable); }
    }
    private LlmProviderException Failure(LlmFailureKind kind) => new(kind, "Groq", options.Model);
    private sealed record GroqRequest([property: JsonPropertyName("model")] string Model, [property: JsonPropertyName("messages")] GroqMessage[] Messages,
        [property: JsonPropertyName("max_completion_tokens")] int MaxTokens, [property: JsonPropertyName("temperature")] double Temperature);
    private sealed record GroqMessage([property: JsonPropertyName("role")] string Role, [property: JsonPropertyName("content")] string Content);
    private sealed record GroqResponse([property: JsonPropertyName("choices")] GroqChoice[]? Choices);
    private sealed record GroqChoice([property: JsonPropertyName("message")] GroqResponseMessage? Message);
    private sealed record GroqResponseMessage([property: JsonPropertyName("content")] string? Content);
}

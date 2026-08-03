using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Diagnostics;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.Json;
using IFS.AIWeb.Application;
using IFS.AIWeb.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IFS.AIWeb.Infrastructure;

public sealed class GroqOptions
{
    public string BaseUrl { get; set; } = "https://api.groq.com/openai/v1/";
    public string Model { get; set; } = "openai/gpt-oss-120b";
    public string ApiKey { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 30;
    public int MaxOutputTokens { get; set; } = 900;
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
    public Task<SummaryRecord?> GetSuccessfulForFeedbackAsync(Guid id, Guid userId, DateTimeOffset now, CancellationToken ct) =>
        db.SummaryRecords.FromSqlInterpolated($"""
            SELECT * FROM summary_records
            WHERE id = {id} AND user_id = {userId} AND status = 'Succeeded' AND expires_at_utc > {now}
            FOR UPDATE
            """).SingleOrDefaultAsync(ct);
}

public sealed class GroqSummarizer(HttpClient client, GroqOptions options, ILogger<GroqSummarizer> logger) : ILlmSummarizer
{
    public async Task<LlmSummary> SummarizeAsync(PromptEnvelope prompt, CancellationToken ct)
    {
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            var started = Stopwatch.GetTimestamp();
            try
            {
                using var request = CreateRequest(prompt); using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
                var requestId = ProviderRequestId(response);
                if (!response.IsSuccessStatusCode)
                {
                    var metadata = await SafeErrorMetadataAsync(response, ct); var category = FailureCategory(response.StatusCode, metadata);
                    var retryable = response.StatusCode == HttpStatusCode.BadRequest && category == LlmFailureCategory.StructuredOutputGeneration;
                    if (retryable && attempt == 1) { LogFailure(category, response.StatusCode, metadata, requestId, true, attempt, prompt.Version, started); continue; }
                    throw Failure(FailureKind(response.StatusCode), category, response.StatusCode, metadata, requestId, attempt > 1, attempt, prompt.Version, started);
                }
                GroqResponse? body;
                try { body = await response.Content.ReadFromJsonAsync<GroqResponse>(cancellationToken: ct); }
                catch (JsonException ex) { throw Failure(LlmFailureKind.InvalidResponse, LlmFailureCategory.ResponseParsing, response.StatusCode, null, requestId, attempt > 1, attempt, prompt.Version, started, ex); }
                var content = body?.Choices?.FirstOrDefault()?.Message?.Content;
                if (string.IsNullOrWhiteSpace(content)) throw Failure(LlmFailureKind.InvalidResponse, LlmFailureCategory.ResponseParsing, response.StatusCode, null, requestId, attempt > 1, attempt, prompt.Version, started);
                StructuredSummary? structured;
                try { structured = JsonSerializer.Deserialize<StructuredSummary>(content); }
                catch (JsonException ex) { throw Failure(LlmFailureKind.InvalidResponse, LlmFailureCategory.ResponseParsing, response.StatusCode, null, requestId, attempt > 1, attempt, prompt.Version, started, ex); }
                if (structured?.Additional is { Count: > 0 }) throw Failure(LlmFailureKind.InvalidResponse, LlmFailureCategory.SchemaValidation, response.StatusCode, null, requestId, attempt > 1, attempt, prompt.Version, started);
                var quality = structured?.Quality switch
                {
                    "sufficient" when !string.IsNullOrWhiteSpace(structured.Summary) => SummaryContentQuality.Sufficient,
                    "insufficient" when string.IsNullOrEmpty(structured.Summary) => SummaryContentQuality.Insufficient,
                    _ => throw Failure(LlmFailureKind.InvalidResponse, LlmFailureCategory.SchemaValidation, response.StatusCode, null, requestId, attempt > 1, attempt, prompt.Version, started)
                };
                return new(structured!.Summary, quality, "Groq", options.Model);
            }
            catch (OperationCanceledException ex) when (!ct.IsCancellationRequested) { throw Failure(LlmFailureKind.Timeout, LlmFailureCategory.Timeout, null, null, null, attempt > 1, attempt, prompt.Version, started, ex); }
            catch (HttpRequestException ex) { throw Failure(LlmFailureKind.Unavailable, LlmFailureCategory.Network, null, null, null, attempt > 1, attempt, prompt.Version, started, ex); }
        }
        throw new InvalidOperationException("Unreachable provider retry state.");
    }
    private HttpRequestMessage CreateRequest(PromptEnvelope prompt)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions"); request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
        request.Content = JsonContent.Create(new GroqRequest(options.Model,
            [new("system", prompt.SystemInstruction), new("user", prompt.UserContent)], options.MaxOutputTokens, 0, "low",
            new("json_schema", new("summary_result", true, new("object", false,
                new Dictionary<string, JsonSchemaProperty> { ["quality"] = new("string", ["sufficient", "insufficient"]), ["summary"] = new("string", null) },
                ["quality", "summary"]))))); return request;
    }
    private LlmProviderException Failure(LlmFailureKind kind, LlmFailureCategory category, HttpStatusCode? status, SafeProviderError? metadata,
        string? requestId, bool retryOccurred, int attempt, string promptVersion, long started, Exception? inner = null)
    {
        LogFailure(category, status, metadata, requestId, retryOccurred, attempt, promptVersion, started);
        return new(kind, category, "Groq", options.Model, status is null ? null : (int)status, metadata?.Type, metadata?.Code, requestId, retryOccurred, attempt, inner);
    }
    private void LogFailure(LlmFailureCategory category, HttpStatusCode? status, SafeProviderError? metadata, string? requestId,
        bool retryOccurred, int attempt, string promptVersion, long started) =>
        logger.LogWarning("Summarization provider failure; Category {FailureCategory}; ProviderStatus {ProviderStatus}; ErrorType {ErrorType}; ErrorCode {ErrorCode}; RetryOccurred {RetryOccurred}; Attempt {Attempt}; DurationMs {DurationMs}; PromptVersion {PromptVersion}; TraceId {TraceId}; ProviderRequestId {ProviderRequestId}",
            category, status is null ? null : (int)status, metadata?.Type, metadata?.Code, retryOccurred, attempt,
            (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds, promptVersion, Activity.Current?.TraceId.ToString(), requestId);
    private static LlmFailureKind FailureKind(HttpStatusCode status) => status switch
    { HttpStatusCode.TooManyRequests => LlmFailureKind.RateLimited, HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => LlmFailureKind.Configuration, _ when (int)status >= 500 => LlmFailureKind.Unavailable, _ => LlmFailureKind.InvalidResponse };
    private static LlmFailureCategory FailureCategory(HttpStatusCode status, SafeProviderError metadata) => status switch
    {
        HttpStatusCode.BadRequest when metadata.IsStructuredOutputGeneration => LlmFailureCategory.StructuredOutputGeneration,
        HttpStatusCode.BadRequest => LlmFailureCategory.InvalidRequest,
        HttpStatusCode.TooManyRequests => LlmFailureCategory.ProviderRateLimit,
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => LlmFailureCategory.Configuration,
        _ when (int)status >= 500 => LlmFailureCategory.ProviderServer,
        _ => LlmFailureCategory.InvalidRequest
    };
    private static async Task<SafeProviderError> SafeErrorMetadataAsync(HttpResponseMessage response, CancellationToken ct)
    {
        const int maxBytes = 16_384; await using var stream = await response.Content.ReadAsStreamAsync(ct); var buffer = new byte[maxBytes + 1]; var length = 0;
        while (length < buffer.Length) { var read = await stream.ReadAsync(buffer.AsMemory(length, buffer.Length - length), ct); if (read == 0) break; length += read; }
        if (length > maxBytes) return new(null, null, false);
        try
        {
            using var document = JsonDocument.Parse(buffer.AsMemory(0, length)); if (!document.RootElement.TryGetProperty("error", out var error) || error.ValueKind != JsonValueKind.Object) return new(null, null, false);
            var type = SafeValue(error, "type"); var code = SafeValue(error, "code"); var hasFailedGeneration = error.TryGetProperty("failed_generation", out _);
            var schemaMismatch = error.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String && message.GetString() == "Generated JSON does not match the expected schema. Please adjust your prompt.";
            var knownCode = code is "json_validate_failed" or "structured_output_validation_failed"; return new(type, code, type == "invalid_request_error" && (hasFailedGeneration || schemaMismatch || knownCode));
        }
        catch (JsonException) { return new(null, null, false); }
    }
    private static string? SafeValue(JsonElement error, string name)
    {
        if (!error.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String) return null; var text = value.GetString();
        return text is { Length: > 0 and <= 64 } && text.All(character => char.IsLetterOrDigit(character) || character is '_' or '-' or '.') ? text : null;
    }
    private static string? ProviderRequestId(HttpResponseMessage response)
    {
        foreach (var name in new[] { "x-request-id", "request-id" }) if (response.Headers.TryGetValues(name, out var values))
        { var value = values.FirstOrDefault(); if (value is { Length: > 0 and <= 128 } && value.All(character => char.IsLetterOrDigit(character) || character is '_' or '-' or '.')) return value; }
        return null;
    }
    private sealed record SafeProviderError(string? Type, string? Code, bool IsStructuredOutputGeneration);
    private sealed record GroqRequest([property: JsonPropertyName("model")] string Model, [property: JsonPropertyName("messages")] GroqMessage[] Messages,
        [property: JsonPropertyName("max_completion_tokens")] int MaxTokens, [property: JsonPropertyName("temperature")] double Temperature,
        [property: JsonPropertyName("reasoning_effort")] string ReasoningEffort,
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

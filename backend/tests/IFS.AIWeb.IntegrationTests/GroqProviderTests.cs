using System.Net;
using System.Text;
using IFS.AIWeb.Application;
using IFS.AIWeb.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;

namespace IFS.AIWeb.IntegrationTests;

public sealed class GroqProviderTests
{
    [Fact] public async Task Request_UsesConfiguredModelTokenLimitAndBearerWithoutExposingValue()
    { var handler = new CaptureHandler(HttpStatusCode.OK, "{\"choices\":[{\"message\":{\"content\":\"{\\\"quality\\\":\\\"sufficient\\\",\\\"summary\\\":\\\"safe summary\\\"}\"}}]}"); var provider = Provider(handler); var result = await provider.SummarizeAsync(new("summary-v3", "system", "<source_text>x</source_text>", IFS.AIWeb.Domain.SummaryLanguage.English), default); Assert.Equal("safe summary", result.Text); Assert.Equal(SummaryContentQuality.Sufficient, result.Quality); Assert.Equal("Bearer", handler.Request!.Headers.Authorization!.Scheme); Assert.Equal("https://api.groq.com/openai/v1/chat/completions", handler.Request.RequestUri!.ToString()); var body = handler.Body!; Assert.Contains("openai/gpt-oss-120b", body); Assert.Contains("max_completion_tokens\":500", body); Assert.Contains("temperature\":0", body); Assert.Contains("reasoning_effort\":\"low\"", body); Assert.Contains("response_format", body); Assert.Contains("json_schema", body); Assert.Contains("strict\":true", body); Assert.Contains("additionalProperties\":false", body); Assert.Contains("required\":[\"quality\",\"summary\"]", body); Assert.Contains("quality", body); Assert.Contains("sufficient", body); Assert.Contains("insufficient", body); Assert.Contains("system", body); Assert.Contains("source_text", body); Assert.DoesNotContain("test-key", body); }
    [Fact] public async Task InsufficientStructuredResponse_IsReturnedWithoutSummary()
    { var provider = Provider(new CaptureHandler(HttpStatusCode.OK, "{\"choices\":[{\"message\":{\"content\":\"{\\\"quality\\\":\\\"insufficient\\\",\\\"summary\\\":\\\"\\\"}\"}}]}")); var result = await provider.SummarizeAsync(new("summary-v3", "system", "source", IFS.AIWeb.Domain.SummaryLanguage.English), default); Assert.Equal(SummaryContentQuality.Insufficient, result.Quality); Assert.Equal(string.Empty, result.Text); }
    [Theory] [InlineData("{\"summary\":\"text\"}")] [InlineData("{\"quality\":\"unknown\",\"summary\":\"text\"}")] [InlineData("{\"quality\":\"sufficient\",\"summary\":\"\"}")] [InlineData("{\"quality\":\"insufficient\",\"summary\":\"invented\"}")] [InlineData("{\"quality\":\"sufficient\",\"summary\":\"text\",\"extra\":true}")]
    public async Task InvalidStructuredResult_FailsClosed(string content)
    { var encoded = System.Text.Json.JsonSerializer.Serialize(new { choices = new[] { new { message = new { content } } } }); var ex = await Assert.ThrowsAsync<LlmProviderException>(() => Provider(new CaptureHandler(HttpStatusCode.OK, encoded)).SummarizeAsync(new("summary-v3", "system", "source", IFS.AIWeb.Domain.SummaryLanguage.English), default)); Assert.Equal(LlmFailureKind.InvalidResponse, ex.Kind); }
    [Theory] [InlineData(HttpStatusCode.TooManyRequests, LlmFailureKind.RateLimited)] [InlineData(HttpStatusCode.ServiceUnavailable, LlmFailureKind.Unavailable)] [InlineData(HttpStatusCode.Unauthorized, LlmFailureKind.Configuration)]
    public async Task NonSuccess_IsMappedWithoutRawBody(HttpStatusCode status, LlmFailureKind expected)
    { var provider = Provider(new CaptureHandler(status, "sensitive provider detail")); var ex = await Assert.ThrowsAsync<LlmProviderException>(() => provider.SummarizeAsync(new("v", "s", "u", IFS.AIWeb.Domain.SummaryLanguage.Turkish), default)); Assert.Equal(expected, ex.Kind); Assert.DoesNotContain("sensitive", ex.Message); }
    [Fact] public async Task MalformedResponse_IsRejected()
    { var provider = Provider(new CaptureHandler(HttpStatusCode.OK, "{}")); var ex = await Assert.ThrowsAsync<LlmProviderException>(() => provider.SummarizeAsync(new("v", "s", "u", IFS.AIWeb.Domain.SummaryLanguage.Turkish), default)); Assert.Equal(LlmFailureKind.InvalidResponse, ex.Kind); }
    [Fact] public async Task CallerCancellation_Propagates()
    { using var cts = new CancellationTokenSource(); cts.Cancel(); var provider = Provider(new CaptureHandler(HttpStatusCode.OK, "{}")); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.SummarizeAsync(new("v", "s", "u", IFS.AIWeb.Domain.SummaryLanguage.Turkish), cts.Token)); }
    [Fact] public async Task StructuredOutput400_IsRetriedOnceAndCanSucceed()
    {
        var handler = new SequenceHandler(
            Response(HttpStatusCode.BadRequest, "{\"error\":{\"message\":\"Generated JSON does not match the expected schema. Please adjust your prompt.\",\"type\":\"invalid_request_error\"}}"),
            Response(HttpStatusCode.OK, "{\"choices\":[{\"message\":{\"content\":\"{\\\"quality\\\":\\\"sufficient\\\",\\\"summary\\\":\\\"safe summary\\\"}\"}}]}")
        );
        var result = await Provider(handler).SummarizeAsync(new("summary-v3", "system", "source", IFS.AIWeb.Domain.SummaryLanguage.English), default);
        Assert.Equal("safe summary", result.Text); Assert.Equal(2, handler.Calls);
    }
    [Fact] public async Task StructuredOutput400_IsRetriedAtMostOnce()
    {
        var handler = new SequenceHandler(Retryable400(), Retryable400());
        var ex = await Assert.ThrowsAsync<LlmProviderException>(() => Provider(handler).SummarizeAsync(new("summary-v3", "system", "source", IFS.AIWeb.Domain.SummaryLanguage.English), default));
        Assert.Equal(2, handler.Calls); Assert.Equal(LlmFailureCategory.StructuredOutputGeneration, ex.Category); Assert.True(ex.RetryOccurred); Assert.Equal(2, ex.AttemptNumber);
    }
    [Theory]
    [InlineData("{\"error\":{\"message\":\"ordinary invalid request\",\"type\":\"invalid_request_error\"}}")]
    [InlineData("{\"error\":{\"message\":\"authentication failed\",\"type\":\"authentication_error\"}}")]
    public async Task Ordinary400_IsNeverRetried(string body)
    {
        var handler = new SequenceHandler(Response(HttpStatusCode.BadRequest, body));
        var ex = await Assert.ThrowsAsync<LlmProviderException>(() => Provider(handler).SummarizeAsync(new("v", "s", "u", IFS.AIWeb.Domain.SummaryLanguage.English), default));
        Assert.Equal(1, handler.Calls); Assert.Equal(LlmFailureCategory.InvalidRequest, ex.Category);
    }
    [Theory] [InlineData(HttpStatusCode.TooManyRequests)] [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task Provider429And5xx_AreNeverRetried(HttpStatusCode status)
    { var handler = new SequenceHandler(Response(status, "{}")); await Assert.ThrowsAsync<LlmProviderException>(() => Provider(handler).SummarizeAsync(new("v", "s", "u", IFS.AIWeb.Domain.SummaryLanguage.English), default)); Assert.Equal(1, handler.Calls); }
    [Theory] [InlineData(false, LlmFailureCategory.Network)] [InlineData(true, LlmFailureCategory.Timeout)]
    public async Task NetworkAndTimeout_AreNotRetried(bool timeout, LlmFailureCategory category)
    { var handler = new ThrowHandler(timeout); var ex = await Assert.ThrowsAsync<LlmProviderException>(() => Provider(handler).SummarizeAsync(new("v", "s", "u", IFS.AIWeb.Domain.SummaryLanguage.English), default)); Assert.Equal(1, handler.Calls); Assert.Equal(category, ex.Category); }
    [Fact] public async Task FailureLog_ContainsSafeCategoryWithoutSensitiveProviderData()
    { const string sentinel = "RAW-PRIVATE-RESPONSE"; var logger = new CaptureLogger(); await Assert.ThrowsAsync<LlmProviderException>(() => Provider(new CaptureHandler(HttpStatusCode.BadRequest, $"{{\"error\":{{\"message\":\"{sentinel}\",\"type\":\"invalid_request_error\"}}}}"), logger).SummarizeAsync(new("summary-v3", "system", "SOURCE-PRIVATE", IFS.AIWeb.Domain.SummaryLanguage.English), default)); var log = Assert.Single(logger.Messages); Assert.Contains("InvalidRequest", log); Assert.Contains("summary-v3", log); Assert.DoesNotContain(sentinel, log); Assert.DoesNotContain("SOURCE-PRIVATE", log); Assert.DoesNotContain("test-key", log); Assert.DoesNotContain("Bearer", log); }
    private static GroqSummarizer Provider(HttpMessageHandler handler, ILogger<GroqSummarizer>? logger = null) => new(new HttpClient(handler) { BaseAddress = new("https://api.groq.com/openai/v1/") }, new() { ApiKey = "test-key", Model = "openai/gpt-oss-120b", MaxOutputTokens = 500 }, logger ?? NullLogger<GroqSummarizer>.Instance);
    private static HttpResponseMessage Response(HttpStatusCode status, string body) => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    private static HttpResponseMessage Retryable400() => Response(HttpStatusCode.BadRequest, "{\"error\":{\"type\":\"invalid_request_error\",\"failed_generation\":\"omitted\"}}");
    private sealed class CaptureHandler(HttpStatusCode status, string responseBody) : HttpMessageHandler
    { public HttpRequestMessage? Request { get; private set; } public string? Body { get; private set; } protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) { Request = request; Body = await request.Content!.ReadAsStringAsync(ct); return new(status) { Content = new StringContent(responseBody, Encoding.UTF8, "application/json") }; } }
    private sealed class SequenceHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    { private readonly Queue<HttpResponseMessage> remaining = new(responses); public int Calls { get; private set; } protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) { Calls++; return Task.FromResult(remaining.Dequeue()); } }
    private sealed class ThrowHandler(bool timeout) : HttpMessageHandler
    { public int Calls { get; private set; } protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) { Calls++; return timeout ? Task.FromException<HttpResponseMessage>(new TaskCanceledException("timeout")) : Task.FromException<HttpResponseMessage>(new HttpRequestException("network")); } }
    private sealed class CaptureLogger : ILogger<GroqSummarizer>
    { public List<string> Messages { get; } = []; public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null; public bool IsEnabled(LogLevel logLevel) => true; public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception)); }
}

using System.Net;
using System.Text;
using IFS.AIWeb.Application;
using IFS.AIWeb.Infrastructure;

namespace IFS.AIWeb.IntegrationTests;

public sealed class GroqProviderTests
{
    [Fact] public async Task Request_UsesConfiguredModelTokenLimitAndBearerWithoutExposingValue()
    { var handler = new CaptureHandler(HttpStatusCode.OK, "{\"choices\":[{\"message\":{\"content\":\"safe summary\"}}]}"); var provider = Provider(handler); var result = await provider.SummarizeAsync(new("summary-v1", "system", "<source_text>x</source_text>", IFS.AIWeb.Domain.SummaryLanguage.English), default); Assert.Equal("safe summary", result.Text); Assert.Equal("Bearer", handler.Request!.Headers.Authorization!.Scheme); Assert.Equal("https://api.groq.com/openai/v1/chat/completions", handler.Request.RequestUri!.ToString()); var body = handler.Body!; Assert.Contains("openai/gpt-oss-120b", body); Assert.Contains("max_completion_tokens\":500", body); Assert.Contains("system", body); Assert.Contains("source_text", body); Assert.DoesNotContain("test-key", body); }
    [Theory] [InlineData(HttpStatusCode.TooManyRequests, LlmFailureKind.RateLimited)] [InlineData(HttpStatusCode.ServiceUnavailable, LlmFailureKind.Unavailable)] [InlineData(HttpStatusCode.Unauthorized, LlmFailureKind.Configuration)]
    public async Task NonSuccess_IsMappedWithoutRawBody(HttpStatusCode status, LlmFailureKind expected)
    { var provider = Provider(new CaptureHandler(status, "sensitive provider detail")); var ex = await Assert.ThrowsAsync<LlmProviderException>(() => provider.SummarizeAsync(new("v", "s", "u", IFS.AIWeb.Domain.SummaryLanguage.Turkish), default)); Assert.Equal(expected, ex.Kind); Assert.DoesNotContain("sensitive", ex.Message); }
    [Fact] public async Task MalformedResponse_IsRejected()
    { var provider = Provider(new CaptureHandler(HttpStatusCode.OK, "{}")); var ex = await Assert.ThrowsAsync<LlmProviderException>(() => provider.SummarizeAsync(new("v", "s", "u", IFS.AIWeb.Domain.SummaryLanguage.Turkish), default)); Assert.Equal(LlmFailureKind.InvalidResponse, ex.Kind); }
    [Fact] public async Task CallerCancellation_Propagates()
    { using var cts = new CancellationTokenSource(); cts.Cancel(); var provider = Provider(new CaptureHandler(HttpStatusCode.OK, "{}")); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.SummarizeAsync(new("v", "s", "u", IFS.AIWeb.Domain.SummaryLanguage.Turkish), cts.Token)); }
    private static GroqSummarizer Provider(HttpMessageHandler handler) => new(new HttpClient(handler) { BaseAddress = new("https://api.groq.com/openai/v1/") }, new() { ApiKey = "test-key", Model = "openai/gpt-oss-120b", MaxOutputTokens = 500 });
    private sealed class CaptureHandler(HttpStatusCode status, string responseBody) : HttpMessageHandler
    { public HttpRequestMessage? Request { get; private set; } public string? Body { get; private set; } protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) { Request = request; Body = await request.Content!.ReadAsStringAsync(ct); return new(status) { Content = new StringContent(responseBody, Encoding.UTF8, "application/json") }; } }
}

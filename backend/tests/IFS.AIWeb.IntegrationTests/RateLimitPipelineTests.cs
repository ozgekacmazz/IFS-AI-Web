using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using IFS.AIWeb.Application;
using IFS.AIWeb.Domain;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IFS.AIWeb.IntegrationTests;

public sealed class RateLimitPipelineTests
{
    [Fact]
    public async Task ActualPipeline_LimitsOnlyPostPerStableAuthenticatedUser()
    {
        await using var factory = new PipelineFactory();
        var first = Client(factory, "10000000-0000-0000-0000-000000000001"); var second = Client(factory, "20000000-0000-0000-0000-000000000002");
        for (var index = 0; index < 5; index++)
        {
            Assert.Equal(HttpStatusCode.OK, (await first.GetAsync("/api/summaries/recent")).StatusCode);
            var created = await first.PostAsJsonAsync("/api/summaries", new { text = $"valid source {index}", language = "English" });
            Assert.Equal(HttpStatusCode.OK, created.StatusCode);
            var createdBody = await created.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            Assert.Equal(HttpStatusCode.OK, (await first.PutAsJsonAsync($"/api/summaries/{createdBody.GetProperty("id").GetGuid()}/feedback", new { value = "Useful" })).StatusCode);
        }
        var rejected = await first.PostAsJsonAsync("/api/summaries", new { text = "sixth source", language = "English" });
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        var header = int.Parse(rejected.Headers.GetValues("Retry-After").Single(), System.Globalization.CultureInfo.InvariantCulture);
        var body = await rejected.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.True(header > 0); Assert.Equal(header, body.GetProperty("retryAfterSeconds").GetInt32());
        Assert.Equal(HttpStatusCode.OK, (await second.PostAsJsonAsync("/api/summaries", new { text = "independent source", language = "English" })).StatusCode);
        var decisions = factory.Services.GetRequiredService<RateLogSink>().Messages.Where(message => message.Contains("Summary rate limit", StringComparison.Ordinal)).ToArray();
        var partitions = decisions.Select(message => System.Text.RegularExpressions.Regex.Match(message, "Partition ([A-F0-9]{12})").Groups[1].Value).ToArray();
        Assert.Equal(7, decisions.Length); Assert.Equal(2, partitions.Distinct().Count()); Assert.Equal(6, partitions.Count(value => value == partitions[0])); Assert.Single(decisions, message => message.Contains("Decision Rejected", StringComparison.Ordinal));
    }

    private static HttpClient Client(WebApplicationFactory<Program> factory, string user)
    { var client = factory.CreateClient(); client.DefaultRequestHeaders.Add("X-Test-User", user); return client; }

    private sealed class PipelineFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing").UseSetting("ConnectionStrings:PostgreSql", "Host=unused;Database=unused;Username=unused;Password=unused")
                .UseSetting("Jwt:SigningKey", "pipeline-test-only-signing-key-32-bytes");
            builder.ConfigureTestServices(services =>
            {
                services.AddAuthentication(options => { options.DefaultAuthenticateScheme = "PipelineTest"; options.DefaultChallengeScheme = "PipelineTest"; })
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("PipelineTest", _ => { });
                services.PostConfigure<AuthorizationOptions>(options => options.DefaultPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
                services.RemoveAll<ILlmSummarizer>(); services.AddSingleton<ILlmSummarizer, FakeLlm>();
                services.RemoveAll<ISummaryRepository>(); services.AddSingleton<ISummaryRepository, MemorySummaries>();
                services.RemoveAll<IUnitOfWork>(); services.AddSingleton<IUnitOfWork, FakeUnit>();
                services.AddSingleton<RateLogSink>(); services.AddSingleton<ILoggerProvider, CaptureProvider>();
            });
        }
    }
    private sealed class TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-Test-User", out var value)) return Task.FromResult(AuthenticateResult.NoResult());
            var claims = new[] { new Claim("sub", value.ToString()), new Claim(ClaimTypes.NameIdentifier, value.ToString()) };
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name)), Scheme.Name)));
        }
    }
    private sealed class FakeLlm : ILlmSummarizer { public Task<LlmSummary> SummarizeAsync(PromptEnvelope prompt, CancellationToken ct) => Task.FromResult(new LlmSummary("safe", SummaryContentQuality.Sufficient, "Fake", "fake")); }
    private sealed class MemorySummaries : ISummaryRepository
    { private readonly List<SummaryRecord> items = []; public void Add(SummaryRecord record) { lock (items) items.Add(record); } public Task<IReadOnlyList<SummaryRecord>> GetRecentSuccessfulAsync(Guid userId, int limit, DateTimeOffset now, CancellationToken ct) { lock (items) return Task.FromResult<IReadOnlyList<SummaryRecord>>(items.Where(x => x.UserId == userId && x.Status == SummaryStatus.Succeeded && x.ExpiresAtUtc > now).Take(limit).ToArray()); } public Task<SummaryRecord?> GetSuccessfulDetailAsync(Guid id, Guid userId, DateTimeOffset now, CancellationToken ct) { lock (items) return Task.FromResult(items.SingleOrDefault(x => x.Id == id && x.UserId == userId && x.Status == SummaryStatus.Succeeded && x.ExpiresAtUtc > now)); } public Task<SummaryRecord?> GetSuccessfulForFeedbackAsync(Guid id, Guid userId, DateTimeOffset now, CancellationToken ct) => GetSuccessfulDetailAsync(id, userId, now, ct); }
    private sealed class FakeUnit : IUnitOfWork { public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask; public Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct) => action(ct); }
    private sealed class RateLogSink { public List<string> Messages { get; } = []; }
    private sealed class CaptureProvider(RateLogSink sink) : ILoggerProvider
    { public ILogger CreateLogger(string categoryName) => new CaptureLogger(categoryName, sink); public void Dispose() { } }
    private sealed class CaptureLogger(string category, RateLogSink sink) : ILogger
    { public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null; public bool IsEnabled(LogLevel logLevel) => category == "SummaryRateLimit"; public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { if (IsEnabled(logLevel)) lock (sink.Messages) sink.Messages.Add(formatter(state, exception)); } }
}

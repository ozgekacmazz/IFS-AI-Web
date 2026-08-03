using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using IFS.AIWeb.Application;
using IFS.AIWeb.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace IFS.AIWeb.IntegrationTests;

public sealed class RequestSizeKestrelTests(RequestSizeKestrelFactory factory) : IClassFixture<RequestSizeKestrelFactory>
{
    private const int BodyLimit = 128 * 1024;

    [Fact]
    public async Task Kestrel_EnforcesByteLimitWhileApplicationEnforcesDecodedTextLimit()
    {
        await AssertStatusForExactAsciiBody(BodyLimit - 1, HttpStatusCode.BadRequest, "below");
        await AssertStatusForExactAsciiBody(BodyLimit, HttpStatusCode.BadRequest, "exact");
        await AssertStatusForExactAsciiBody(BodyLimit + 1, HttpStatusCode.RequestEntityTooLarge, "above");

        var oversizedAscii = Serialize(new string('a', 140_000), "English");
        Assert.Equal(140_032, oversizedAscii.Length);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, await SendAsAuthenticatedUser(oversizedAscii, "ascii"));

        var oversizedUnicode = Serialize(string.Concat(Enumerable.Repeat("Türkçe 🙂 漢字 ", 8_000)), "Turkish");
        Assert.Equal(344_032, oversizedUnicode.Length);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, await SendAsAuthenticatedUser(oversizedUnicode, "unicode"));

        var overTextWithinBodyLimit = Serialize("valid " + new string('a', 11_995), "English");
        Assert.Equal(12_033, overTextWithinBodyLimit.Length);
        Assert.Equal(HttpStatusCode.BadRequest, await SendAsAuthenticatedUser(overTextWithinBodyLimit, "textlimit"));

        Assert.Equal(0, factory.Provider.Calls);
    }

    private async Task AssertStatusForExactAsciiBody(int bodyBytes, HttpStatusCode expected, string userPrefix)
    {
        var body = ExactAsciiBody(bodyBytes);
        Assert.Equal(bodyBytes, body.Length);
        Assert.Equal(expected, await SendAsAuthenticatedUser(body, userPrefix));
    }

    private async Task<HttpStatusCode> SendAsAuthenticatedUser(byte[] body, string prefix)
    {
        using var client = factory.Client();
        var username = $"size{prefix}{Guid.NewGuid():N}"[..24]; const string password = "Secure123!";
        using var register = await client.PostAsJsonAsync("/api/auth/register", new { username, firstName = "Test", lastName = "User", password, passwordConfirmation = password });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        using var login = await client.PostAsJsonAsync("/api/auth/login", new { username, password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var session = (await login.Content.ReadFromJsonAsync<Session>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        using var content = new ByteArrayContent(body); content.Headers.ContentType = new("application/json") { CharSet = "utf-8" };
        using var response = await client.PostAsync("/api/summaries", content);
        return response.StatusCode;
    }

    private static byte[] ExactAsciiBody(int targetBytes)
    {
        const string prefix = "{\"text\":\""; const string suffix = "\",\"language\":\"English\"}";
        var textLength = targetBytes - Encoding.UTF8.GetByteCount(prefix) - Encoding.UTF8.GetByteCount(suffix);
        Assert.True(textLength > 0);
        return Encoding.UTF8.GetBytes(prefix + new string('a', textLength) + suffix);
    }

    private static byte[] Serialize(string text, string language) =>
        JsonSerializer.SerializeToUtf8Bytes(new { text, language });

    private sealed record Session(string AccessToken);
}

public sealed class RequestSizeKestrelFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18-alpine").Build();
    public CountingFakeLlmSummarizer Provider { get; } = new();

    public RequestSizeKestrelFactory() => UseKestrel(0);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing")
            .UseSetting("ConnectionStrings:PostgreSql", postgres.GetConnectionString())
            .UseSetting("Jwt:Issuer", "tests")
            .UseSetting("Jwt:Audience", "tests-spa")
            .UseSetting("Jwt:SigningKey", "integration-test-only-signing-key-32-bytes")
            .UseSetting("Cors:AllowedOrigins:0", "https://spa.test");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ILlmSummarizer>();
            services.AddSingleton<ILlmSummarizer>(Provider);
        });
    }

    public async Task InitializeAsync()
    {
        await postgres.StartAsync();
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AuthDbContext>().Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await postgres.DisposeAsync();
    }

    public HttpClient Client() => base.CreateClient();
}

public sealed class CountingFakeLlmSummarizer : ILlmSummarizer
{
    private int calls;
    public int Calls => Volatile.Read(ref calls);
    public Task<LlmSummary> SummarizeAsync(PromptEnvelope prompt, CancellationToken ct)
    {
        Interlocked.Increment(ref calls);
        return Task.FromResult(new LlmSummary("Test özeti", SummaryContentQuality.Sufficient, "FakeGroq", "fake-model"));
    }
}

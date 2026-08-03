using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using IFS.AIWeb.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;
using IFS.AIWeb.Application;

namespace IFS.AIWeb.IntegrationTests;

public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18-alpine").Build();
    protected override void ConfigureWebHost(IWebHostBuilder builder) { builder.UseEnvironment("Testing").UseSetting("ConnectionStrings:PostgreSql", postgres.GetConnectionString()).UseSetting("Jwt:Issuer", "tests").UseSetting("Jwt:Audience", "tests-spa").UseSetting("Jwt:SigningKey", "integration-test-only-signing-key-32-bytes").UseSetting("Cors:AllowedOrigins:0", "https://spa.test"); builder.ConfigureTestServices(services => { services.RemoveAll<ILlmSummarizer>(); services.AddSingleton<ILlmSummarizer, FakeLlmSummarizer>(); }); }
    public async Task InitializeAsync() { await postgres.StartAsync(); using var scope = Services.CreateScope(); await scope.ServiceProvider.GetRequiredService<AuthDbContext>().Database.MigrateAsync(); }
    public new async Task DisposeAsync() { await base.DisposeAsync(); await postgres.DisposeAsync(); }
    public HttpClient Client(bool cookies = true) => CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = cookies });
}

public sealed class FakeLlmSummarizer : ILlmSummarizer
{
    public Task<LlmSummary> SummarizeAsync(PromptEnvelope prompt, CancellationToken ct)
    { if (prompt.UserContent.Contains("provider fail", StringComparison.Ordinal)) throw new LlmProviderException(LlmFailureKind.Unavailable); if (prompt.UserContent.Contains("provider insufficient", StringComparison.Ordinal)) return Task.FromResult(new LlmSummary(null, SummaryContentQuality.Insufficient, "FakeGroq", "fake-model")); return Task.FromResult(new LlmSummary("Test özeti", SummaryContentQuality.Sufficient, "FakeGroq", "fake-model")); }
}

public sealed class AuthApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact] public async Task Health_RemainsAnonymous()
    { using var response = await factory.Client().GetAsync("/health"); Assert.Equal(HttpStatusCode.OK, response.StatusCode); }
    [Fact] public async Task Registration_IsUser_Hashed_AndDuplicateIsConflict()
    {
        var client = factory.Client(); var password = "Secure123!"; var response = await Register(client, "CaseUser", password); Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>(); var user = await db.Users.SingleAsync(x => x.NormalizedUsername == "CASEUSER"); Assert.True(user.IsActive); Assert.Equal("User", user.Role.ToString()); Assert.NotEqual(password, user.PasswordHash);
        var duplicate = await Register(client, "caseuser", password); Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode); Assert.DoesNotContain("Postgres", await duplicate.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }
    [Fact] public async Task InvalidRegistration_ReturnsFieldErrorsAndNeverLeaksAs500()
    {
        var response = await factory.Client().PostAsJsonAsync("/api/auth/register", new { username = " ", firstName = "", lastName = "", password = "weak", passwordConfirmation = (string?)null });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("username", body); Assert.Contains("firstName", body); Assert.Contains("lastName", body); Assert.Contains("passwordConfirmation", body); Assert.DoesNotContain("Exception", body, StringComparison.OrdinalIgnoreCase);
    }
    [Fact] public async Task UnexpectedRegistrationFailure_ReturnsSafeProblemDetails()
    {
        await using var failing = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => { services.RemoveAll<IUnitOfWork>(); services.AddScoped<IUnitOfWork, FailingUnitOfWork>(); }));
        var response = await Register(failing.CreateClient(), "failure" + Guid.NewGuid().ToString("N")[..8], "Secure123!");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode); var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Şu anda istek tamamlanamadı", body); Assert.DoesNotContain("InvalidOperationException", body); Assert.DoesNotContain("Secure123!", body);
    }
    [Fact] public async Task LoginRefreshReuseLogoutAndAuthorization_Work()
    {
        var username = "flow" + Guid.NewGuid().ToString("N")[..8]; var password = "Secure123!"; var client = factory.Client(false); await Register(client, username, password);
        var login = await client.PostAsJsonAsync("/api/auth/login", new { username, password }); Assert.Equal(HttpStatusCode.OK, login.StatusCode); var loginText = await login.Content.ReadAsStringAsync(); Assert.Contains("accessToken", loginText); Assert.DoesNotContain("refreshToken", loginText, StringComparison.OrdinalIgnoreCase);
        var oldCookie = Cookie(login); Assert.Contains("httponly", oldCookie, StringComparison.OrdinalIgnoreCase); var access = (await login.Content.ReadFromJsonAsync<Session>())!.AccessToken;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", access); Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode); Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/auth/admin-check")).StatusCode);
        var refresh = Request(HttpMethod.Post, "/api/auth/refresh", oldCookie); var refreshed = await client.SendAsync(refresh); Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode); var newCookie = Cookie(refreshed); Assert.NotEqual(Value(oldCookie), Value(newCookie));
        var reuse = await client.SendAsync(Request(HttpMethod.Post, "/api/auth/refresh", oldCookie)); Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode); var familyRevoked = await client.SendAsync(Request(HttpMethod.Post, "/api/auth/refresh", newCookie)); Assert.Equal(HttpStatusCode.Unauthorized, familyRevoked.StatusCode);
        var logout = await client.SendAsync(Request(HttpMethod.Post, "/api/auth/logout", newCookie)); Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
    }
    [Fact] public async Task ProtectedEndpointRequiresAuthentication_AndOriginIsEnforced()
    { var client = factory.Client(false); Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode); var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh"); request.Headers.TryAddWithoutValidation("Cookie", "refreshToken=invalid"); request.Headers.TryAddWithoutValidation("Origin", "https://evil.test"); Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(request)).StatusCode); }
    [Fact] public async Task NullLoginPassword_IsSafeUnauthorizedRatherThan500()
    { var response = await factory.Client().PostAsJsonAsync("/api/auth/login", new { username = "member", password = (string?)null }); Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode); Assert.DoesNotContain("Exception", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase); }
    [Fact] public async Task AdminSucceeds_AndInactiveUserCannotUseOldAccessOrRefresh()
    {
        var username = "admin" + Guid.NewGuid().ToString("N")[..8]; var password = "Secure123!"; var client = factory.Client(false); await Register(client, username, password);
        using (var scope = factory.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>(); await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE users SET role = 'Admin' WHERE normalized_username = {username.ToUpperInvariant()}"); }
        var login = await client.PostAsJsonAsync("/api/auth/login", new { username, password }); var session = (await login.Content.ReadFromJsonAsync<Session>())!; var cookie = Cookie(login); client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken); Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/admin-check")).StatusCode);
        using (var scope = factory.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>(); await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE users SET is_active = FALSE WHERE normalized_username = {username.ToUpperInvariant()}"); }
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/auth/me")).StatusCode); Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(Request(HttpMethod.Post, "/api/auth/refresh", cookie))).StatusCode); var inactiveLogin = await client.PostAsJsonAsync("/api/auth/login", new { username, password }); Assert.Equal(HttpStatusCode.Forbidden, inactiveLogin.StatusCode); Assert.Contains("Hesabınız pasife alınmıştır", await inactiveLogin.Content.ReadAsStringAsync());
    }
    [Fact] public async Task ConcurrentRefresh_AllowsOnlyOneRotationAndRevokesTheFamily()
    {
        var username = "race" + Guid.NewGuid().ToString("N")[..8]; var password = "Secure123!"; var client = factory.Client(false); await Register(client, username, password);
        var login = await client.PostAsJsonAsync("/api/auth/login", new { username, password }); var cookie = Cookie(login);
        var calls = new[] { client.SendAsync(Request(HttpMethod.Post, "/api/auth/refresh", cookie)), client.SendAsync(Request(HttpMethod.Post, "/api/auth/refresh", cookie)) };
        var responses = await Task.WhenAll(calls); Assert.Single(responses, x => x.StatusCode == HttpStatusCode.OK); Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Unauthorized);
        var successful = responses.Single(x => x.StatusCode == HttpStatusCode.OK); var replacement = Cookie(successful);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(Request(HttpMethod.Post, "/api/auth/refresh", replacement))).StatusCode);
        foreach (var response in responses) response.Dispose();
    }
    private static Task<HttpResponseMessage> Register(HttpClient client, string username, string password) => client.PostAsJsonAsync("/api/auth/register", new { username, firstName = "Test", lastName = "User", password, passwordConfirmation = password });
    private static string Cookie(HttpResponseMessage response) => response.Headers.GetValues("Set-Cookie").Single(x => x.StartsWith("refreshToken="));
    private static string Value(string cookie) => cookie.Split(';')[0];
    private static HttpRequestMessage Request(HttpMethod method, string path, string cookie) { var request = new HttpRequestMessage(method, path); request.Headers.TryAddWithoutValidation("Cookie", Value(cookie)); request.Headers.TryAddWithoutValidation("Origin", "https://spa.test"); return request; }
    private sealed record Session(string AccessToken);
    private sealed class FailingUnitOfWork : IUnitOfWork
    {
        public Task SaveChangesAsync(CancellationToken ct) => throw new InvalidOperationException("test-only failure");
        public Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct) => action(ct);
    }
}

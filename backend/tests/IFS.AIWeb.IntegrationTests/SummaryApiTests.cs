using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using IFS.AIWeb.Domain;
using IFS.AIWeb.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IFS.AIWeb.IntegrationTests;

public sealed class SummaryApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact] public async Task AnonymousPost_IsRejected() => Assert.Equal(HttpStatusCode.Unauthorized, (await factory.Client().PostAsJsonAsync("/api/summaries", new { text = "hello", language = "English" })).StatusCode);
    [Fact] public async Task ValidRequest_PersistsFullContentForAuthenticatedUserWithExpiry()
    { var (client, userId) = await AuthenticatedClient("summary"); var input = "Full private input text"; var response = await client.PostAsJsonAsync("/api/summaries", new { text = input, language = "Turkish", userId = Guid.NewGuid(), model = "attacker" }); Assert.Equal(HttpStatusCode.OK, response.StatusCode); var body = await response.Content.ReadFromJsonAsync<SummaryDto>(); Assert.NotNull(body); Assert.Equal("Test özeti", body.Summary); var json = await response.Content.ReadAsStringAsync(); Assert.DoesNotContain("token", json, StringComparison.OrdinalIgnoreCase); Assert.DoesNotContain("provider", json, StringComparison.OrdinalIgnoreCase);
      using var scope = factory.Services.CreateScope(); var record = await scope.ServiceProvider.GetRequiredService<AuthDbContext>().SummaryRecords.SingleAsync(x => x.Id == body.Id); Assert.Equal(userId, record.UserId); Assert.Equal(input, record.InputText); Assert.Equal("Test özeti", record.SummaryText); Assert.Equal(record.CreatedAtUtc.AddDays(30), record.ExpiresAtUtc); }
    [Fact] public async Task InvalidAndProviderFailure_ReturnSafeProblemDetails()
    { var (client, _) = await AuthenticatedClient("errors"); var invalid = await client.PostAsJsonAsync("/api/summaries", new { text = " ", language = "Turkish" }); Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode); var failed = await client.PostAsJsonAsync("/api/summaries", new { text = "provider-fail", language = "English" }); Assert.Equal(HttpStatusCode.BadGateway, failed.StatusCode); var text = await failed.Content.ReadAsStringAsync(); Assert.DoesNotContain("provider-fail", text); Assert.DoesNotContain("FakeGroq", text); }
    [Fact] public async Task Recent_ReturnsOnlyOwnLatestThreeNewestFirst()
    { var (first, _) = await AuthenticatedClient("recenta"); var (other, _) = await AuthenticatedClient("recentb"); for (var i = 0; i < 4; i++) { await first.PostAsJsonAsync("/api/summaries", new { text = $"first-{i}", language = "English" }); await Task.Delay(5); } await other.PostAsJsonAsync("/api/summaries", new { text = "other-private", language = "Turkish" }); var recent = await first.GetFromJsonAsync<RecentDto[]>("/api/summaries/recent"); Assert.NotNull(recent); Assert.Equal(3, recent.Length); Assert.Equal("first-3", recent[0].InputText); Assert.Equal("first-1", recent[2].InputText); Assert.DoesNotContain(recent, x => x.InputText.Contains("other")); }
    [Fact] public async Task FiveRequestsAllowedSixthRejectedAndPartitionsAreSeparate()
    { var (first, _) = await AuthenticatedClient("limita"); var (second, _) = await AuthenticatedClient("limitb"); for (var i = 0; i < 5; i++) Assert.Equal(HttpStatusCode.OK, (await first.PostAsJsonAsync("/api/summaries", new { text = $"request-{i}", language = "English" })).StatusCode); var sixth = await first.PostAsJsonAsync("/api/summaries", new { text = "sixth", language = "English" }); Assert.Equal(HttpStatusCode.TooManyRequests, sixth.StatusCode); Assert.True(sixth.Headers.RetryAfter is not null || sixth.Headers.Contains("Retry-After")); Assert.Equal(HttpStatusCode.OK, (await second.PostAsJsonAsync("/api/summaries", new { text = "separate", language = "English" })).StatusCode); }
    [Fact] public async Task InactiveUserWithOldToken_IsForbidden()
    { var (client, userId) = await AuthenticatedClient("inactive"); using (var scope = factory.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<AuthDbContext>().Database.ExecuteSqlInterpolatedAsync($"UPDATE users SET is_active = FALSE WHERE id = {userId}"); Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/summaries", new { text = "valid", language = "English" })).StatusCode); }
    private async Task<(HttpClient Client, Guid UserId)> AuthenticatedClient(string prefix)
    { var client = factory.Client(false); var username = prefix + Guid.NewGuid().ToString("N")[..8]; const string password = "Secure123!"; await client.PostAsJsonAsync("/api/auth/register", new { username, firstName = "Test", lastName = "User", password, passwordConfirmation = password }); var login = await client.PostAsJsonAsync("/api/auth/login", new { username, password }); var session = (await login.Content.ReadFromJsonAsync<Session>())!; client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken); using var scope = factory.Services.CreateScope(); var id = await scope.ServiceProvider.GetRequiredService<AuthDbContext>().Users.Where(x => x.NormalizedUsername == username.ToUpperInvariant()).Select(x => x.Id).SingleAsync(); return (client, id); }
    private sealed record Session(string AccessToken);
    private sealed record SummaryDto(Guid Id, string Summary, string Language, DateTimeOffset CreatedAtUtc, DateTimeOffset ExpiresAtUtc);
    private sealed record RecentDto(Guid Id, string InputText, string Summary, string Language, DateTimeOffset CreatedAtUtc, DateTimeOffset ExpiresAtUtc);
}

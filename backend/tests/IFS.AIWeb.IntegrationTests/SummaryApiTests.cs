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
    { var (client, _) = await AuthenticatedClient("errors"); var invalid = await client.PostAsJsonAsync("/api/summaries", new { text = " ", language = "Turkish" }); Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode); var oneWord = await client.PostAsJsonAsync("/api/summaries", new { text = "Merhaba", language = "Turkish" }); Assert.Equal(HttpStatusCode.BadRequest, oneWord.StatusCode); var failed = await client.PostAsJsonAsync("/api/summaries", new { text = "provider fail", language = "English" }); Assert.Equal(HttpStatusCode.BadGateway, failed.StatusCode); var text = await failed.Content.ReadAsStringAsync(); Assert.DoesNotContain("provider fail", text); Assert.DoesNotContain("FakeGroq", text); }
    [Fact] public async Task ProviderInsufficient_ReturnsSafe422PersistsPrivateFailedAuditAndStaysOutOfRecent()
    { var (client, userId) = await AuthenticatedClient("insufficient"); const string source = "provider insufficient private sentinel"; var response = await client.PostAsJsonAsync("/api/summaries", new { text = source, language = "English" }); Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode); var json = await response.Content.ReadAsStringAsync(); Assert.Contains("insufficient_content", json); Assert.DoesNotContain(source, json); Assert.DoesNotContain("FakeGroq", json); using var scope = factory.Services.CreateScope(); var record = await scope.ServiceProvider.GetRequiredService<AuthDbContext>().SummaryRecords.SingleAsync(x => x.UserId == userId); Assert.Equal(SummaryStatus.Failed, record.Status); Assert.Equal("insufficient_content", record.FailureCode); Assert.Equal(string.Empty, record.InputText); Assert.Null(record.SummaryText); var recent = await client.GetFromJsonAsync<RecentDto[]>("/api/summaries/recent"); Assert.Empty(recent!); }
    [Theory] [InlineData("Toplantı ertelendi.", "Turkish")] [InlineData("Meeting postponed.", "English")]
    public async Task ShortInformativeContent_RemainsSuccessful(string text, string language)
    { var (client, _) = await AuthenticatedClient("short"); Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/summaries", new { text, language })).StatusCode); }
    [Fact] public async Task UnicodeCharacterContractReachesApplicationValidationAndOversizedBodyIsRejected()
    {
        var (turkishClient, _) = await AuthenticatedClient("sizetr"); var turkish = ToLength("Türkçe metin ", 12000);
        Assert.Equal(HttpStatusCode.OK, (await turkishClient.PostAsJsonAsync("/api/summaries", new { text = turkish, language = "Turkish" })).StatusCode);
        var (unicodeClient, _) = await AuthenticatedClient("sizeunicode"); var unicode = ToLength("ab🙂漢字", 12000);
        Assert.Equal(HttpStatusCode.OK, (await unicodeClient.PostAsJsonAsync("/api/summaries", new { text = unicode, language = "English" })).StatusCode);
        var (overCharacterClient, _) = await AuthenticatedClient("sizechars"); var overCharacters = ToLength("ab", 12001);
        Assert.Equal(HttpStatusCode.BadRequest, (await overCharacterClient.PostAsJsonAsync("/api/summaries", new { text = overCharacters, language = "English" })).StatusCode);
        var (oversizedClient, _) = await AuthenticatedClient("sizebody"); var oversizedJson = System.Text.Json.JsonSerializer.Serialize(new { text = new string('a', 140_000), language = "English" });
        using var oversizedContent = new StringContent(oversizedJson, System.Text.Encoding.UTF8, "application/json");
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await oversizedClient.PostAsync("/api/summaries", oversizedContent)).StatusCode);
    }
    [Fact] public async Task OwnerCanRetrieveCompleteSuccessfulDetail()
    { var (client, _) = await AuthenticatedClient("detail"); const string source = "DETAIL-SOURCE complete synthetic text"; var created = await client.PostAsJsonAsync("/api/summaries", new { text = source, language = "English" }); var summary = (await created.Content.ReadFromJsonAsync<SummaryDto>())!; var response = await client.GetAsync($"/api/summaries/{summary.Id}"); Assert.Equal(HttpStatusCode.OK, response.StatusCode); var detail = (await response.Content.ReadFromJsonAsync<DetailDto>())!; Assert.Equal(summary.Id, detail.Id); Assert.Equal(source, detail.InputText); Assert.Equal("Test özeti", detail.Summary); Assert.Equal("English", detail.Language); Assert.Equal("summary-v3", detail.PromptVersion); Assert.True(detail.ExpiresAtUtc > detail.CreatedAtUtc); }
    [Fact] public async Task DetailRejectsAnonymousAndHidesCrossUserFailedExpiredAndMissingRecords()
    {
        var (owner, ownerId) = await AuthenticatedClient("owner"); var (other, _) = await AuthenticatedClient("other");
        var created = (await (await owner.PostAsJsonAsync("/api/summaries", new { text = "owned source", language = "Turkish" })).Content.ReadFromJsonAsync<SummaryDto>())!;
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.Client().GetAsync($"/api/summaries/{created.Id}")).StatusCode);
        var crossUser = await other.GetAsync($"/api/summaries/{created.Id}"); var missing = await other.GetAsync($"/api/summaries/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, crossUser.StatusCode); Assert.Equal(await missing.Content.ReadAsStringAsync(), await crossUser.Content.ReadAsStringAsync());
        await owner.PostAsJsonAsync("/api/summaries", new { text = "provider fail", language = "English" });
        Guid failedId; Guid expiredId;
        using (var scope = factory.Services.CreateScope())
        { var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>(); failedId = await db.SummaryRecords.Where(x => x.UserId == ownerId && x.Status == SummaryStatus.Failed).OrderByDescending(x => x.CreatedAtUtc).Select(x => x.Id).FirstAsync(); var expired = SummaryRecord.Create(ownerId, "expired synthetic source", "expired summary", SummaryLanguage.English, SummaryStatus.Succeeded, "Test", "test", "summary-v1", 1, DateTimeOffset.UtcNow.AddDays(-31)); db.SummaryRecords.Add(expired); await db.SaveChangesAsync(); expiredId = expired.Id; }
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/api/summaries/{failedId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/api/summaries/{expiredId}")).StatusCode);
    }
    [Fact] public async Task Recent_ExcludesIneligibleAndOtherUsersBeforeTakingNewestSeven()
    {
        var (first, firstId) = await AuthenticatedClient("recenta"); var (_, otherId) = await AuthenticatedClient("recentb"); var now = DateTimeOffset.UtcNow;
        var created = Enumerable.Range(0, 8).Select(index => SummaryRecord.Create(firstId, $"first source {index}", $"summary {index}", SummaryLanguage.English, SummaryStatus.Succeeded, "Test", "test", "summary-v3", 1, now.AddMinutes(index))).ToArray();
        var expired = SummaryRecord.Create(firstId, "expired private", "expired summary", SummaryLanguage.English, SummaryStatus.Succeeded, "Test", "test", "summary-v3", 1, now.AddDays(-31));
        var otherSummary = SummaryRecord.Create(otherId, "other private", "other summary", SummaryLanguage.Turkish, SummaryStatus.Succeeded, "Test", "test", "summary-v3", 1, now);
        using (var scope = factory.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>(); db.SummaryRecords.AddRange(created.Append(expired).Append(otherSummary)); await db.SaveChangesAsync(); }
        var response = await first.GetAsync("/api/summaries/recent"); var recent = await response.Content.ReadFromJsonAsync<RecentDto[]>(); Assert.NotNull(recent); Assert.Equal(7, recent.Length); Assert.Equal(created.Reverse().Take(7).Select(x => x.Id), recent.Select(x => x.Id)); Assert.DoesNotContain(recent, item => item.Id == expired.Id || item.Id == otherSummary.Id); Assert.All(recent, item => Assert.True(item.ExpiresAtUtc > DateTimeOffset.UtcNow)); var json = await response.Content.ReadAsStringAsync(); Assert.DoesNotContain("inputText", json); Assert.DoesNotContain("first source", json); Assert.DoesNotContain("other private", json);
    }
    [Fact] public async Task FiveRequestsAllowedSixthRejectedAndPartitionsAreSeparate()
    { var (first, _) = await AuthenticatedClient("limita"); var (second, _) = await AuthenticatedClient("limitb"); for (var i = 0; i < 5; i++) { Assert.Equal(HttpStatusCode.OK, (await first.GetAsync("/api/summaries/recent")).StatusCode); Assert.Equal(HttpStatusCode.OK, (await first.PostAsJsonAsync("/api/summaries", new { text = $"request number {i}", language = "English" })).StatusCode); } var sixth = await first.PostAsJsonAsync("/api/summaries", new { text = "sixth request", language = "English" }); Assert.Equal(HttpStatusCode.TooManyRequests, sixth.StatusCode); var header = int.Parse(sixth.Headers.GetValues("Retry-After").Single(), System.Globalization.CultureInfo.InvariantCulture); var json = await sixth.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(); Assert.True(header > 0); Assert.Equal(header, json.GetProperty("retryAfterSeconds").GetInt32()); Assert.Equal(HttpStatusCode.OK, (await second.PostAsJsonAsync("/api/summaries", new { text = "separate request", language = "English" })).StatusCode); }
    [Fact] public async Task InactiveUserWithOldToken_IsForbidden()
    { var (client, userId) = await AuthenticatedClient("inactive"); using (var scope = factory.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<AuthDbContext>().Database.ExecuteSqlInterpolatedAsync($"UPDATE users SET is_active = FALSE WHERE id = {userId}"); Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/summaries", new { text = "valid", language = "English" })).StatusCode); }
    private static string ToLength(string seed, int length) { var builder = new System.Text.StringBuilder(length + seed.Length); while (builder.Length < length) builder.Append(seed); if (builder.Length > length) builder.Length = length; return builder.ToString(); }
    private async Task<(HttpClient Client, Guid UserId)> AuthenticatedClient(string prefix)
    { var client = factory.Client(false); var username = prefix + Guid.NewGuid().ToString("N")[..8]; const string password = "Secure123!"; await client.PostAsJsonAsync("/api/auth/register", new { username, firstName = "Test", lastName = "User", password, passwordConfirmation = password }); var login = await client.PostAsJsonAsync("/api/auth/login", new { username, password }); var session = (await login.Content.ReadFromJsonAsync<Session>())!; client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken); using var scope = factory.Services.CreateScope(); var id = await scope.ServiceProvider.GetRequiredService<AuthDbContext>().Users.Where(x => x.NormalizedUsername == username.ToUpperInvariant()).Select(x => x.Id).SingleAsync(); return (client, id); }
    private sealed record Session(string AccessToken);
    private sealed record SummaryDto(Guid Id, string Summary, string Language, DateTimeOffset CreatedAtUtc, DateTimeOffset ExpiresAtUtc);
    private sealed record RecentDto(Guid Id, string Summary, string Language, DateTimeOffset CreatedAtUtc, DateTimeOffset ExpiresAtUtc);
    private sealed record DetailDto(Guid Id, string InputText, string Summary, string Language, DateTimeOffset CreatedAtUtc, DateTimeOffset ExpiresAtUtc, string PromptVersion);
}

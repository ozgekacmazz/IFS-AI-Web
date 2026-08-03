using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using IFS.AIWeb.Application;
using IFS.AIWeb.Domain;
using IFS.AIWeb.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IFS.AIWeb.IntegrationTests;

[CollectionDefinition("Admin API", DisableParallelization = true)]
public sealed class AdminApiCollection;

[Collection("Admin API")]
public sealed class AdminApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task EveryAdminEndpoint_RequiresAdminAndActiveUser()
    {
        var normal = await SessionAsync("ordinary", UserRole.User);
        var endpoints = new (HttpMethod Method, string Path, object? Body)[]
        {
            (HttpMethod.Get, "/api/admin/users", null),
            (HttpMethod.Post, "/api/admin/users", NewUser("blocked", "User")),
            (HttpMethod.Patch, $"/api/admin/users/{Guid.NewGuid()}/status", new { isActive = false }),
            (HttpMethod.Put, $"/api/admin/users/{Guid.NewGuid()}/password", new { password = "Changed123!", passwordConfirmation = "Changed123!" }),
            (HttpMethod.Get, "/api/admin/logs", null),
            (HttpMethod.Get, "/api/admin/prompt-info", null),
            (HttpMethod.Get, "/api/admin/statistics/seven-days", null)
        };
        foreach (var endpoint in endpoints)
        {
            using var anonymousRequest = Request(endpoint.Method, endpoint.Path, endpoint.Body);
            Assert.Equal(HttpStatusCode.Unauthorized, (await factory.Client(false).SendAsync(anonymousRequest)).StatusCode);
            using var userRequest = Request(endpoint.Method, endpoint.Path, endpoint.Body);
            Assert.Equal(HttpStatusCode.Forbidden, (await normal.Client.SendAsync(userRequest)).StatusCode);
        }

        var admin = await SessionAsync("inactiveadmin", UserRole.Admin);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            var user = await db.Users.SingleAsync(x => x.Id == admin.UserId); user.Deactivate(DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.Client.GetAsync("/api/admin/users")).StatusCode);
    }

    [Fact]
    public async Task AdminCreatesListsSearchesAndFiltersSafeUsers()
    {
        var admin = await SessionAsync("manager", UserRole.Admin);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var userName = $"Çalışan{suffix}"; var createdUser = await admin.Client.PostAsJsonAsync("/api/admin/users", NewUser(userName, "User"));
        Assert.Equal(HttpStatusCode.Created, createdUser.StatusCode);
        var adminName = $"newadmin{suffix}"; var createdAdmin = await admin.Client.PostAsJsonAsync("/api/admin/users", NewUser(adminName, "Admin"));
        Assert.Equal(HttpStatusCode.Created, createdAdmin.StatusCode);

        var userJson = await createdUser.Content.ReadAsStringAsync();
        Assert.DoesNotContain("password", userJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hash", userJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", userJson, StringComparison.OrdinalIgnoreCase);
        using (var scope = factory.Services.CreateScope())
        {
            var stored = await scope.ServiceProvider.GetRequiredService<AuthDbContext>().Users
                .SingleAsync(x => x.NormalizedUsername == userName.Normalize().ToUpperInvariant());
            Assert.NotEqual("Secure123!", stored.PasswordHash);
        }

        var list = await admin.Client.GetFromJsonAsync<UserList>($"/api/admin/users?page=1&pageSize=1&search={Uri.EscapeDataString(userName.ToLowerInvariant())}&role=User&isActive=true");
        Assert.NotNull(list); Assert.Equal(1, list.Page); Assert.Equal(1, list.PageSize); Assert.True(list.Total >= 1);
        Assert.Single(list.Items); Assert.Equal(userName, list.Items[0].Username); Assert.Equal("User", list.Items[0].Role);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.Client.GetAsync("/api/admin/users?page=0&pageSize=101")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.Client.GetAsync("/api/admin/users?role=Owner")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.Client.GetAsync("/api/admin/users?role=1")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.Client.GetAsync("/api/admin/users?isActive=maybe")).StatusCode);

        var duplicate = await admin.Client.PostAsJsonAsync("/api/admin/users", NewUser(userName.ToLowerInvariant(), "User"));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.Client.PostAsJsonAsync("/api/admin/users", NewUser("badrole" + suffix, "Owner"))).StatusCode);
        var mismatch = NewUser("mismatch" + suffix, "User") with { PasswordConfirmation = "Different123!" };
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.Client.PostAsJsonAsync("/api/admin/users", mismatch)).StatusCode);
    }

    [Fact]
    public async Task StatusChangeRevokesSessionsAndProtectsSelf()
    {
        var admin = await SessionAsync("statusadmin", UserRole.Admin);
        var target = await SessionAsync("statustarget", UserRole.User);
        var deactivate = await admin.Client.PatchAsJsonAsync($"/api/admin/users/{target.UserId}/status", new { isActive = false });
        Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await target.Client.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(target.Cookie)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(target.Username, "Secure123!")).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var tokens = await scope.ServiceProvider.GetRequiredService<AuthDbContext>().RefreshTokens
                .Where(x => x.UserId == target.UserId).ToListAsync();
            Assert.NotEmpty(tokens); Assert.All(tokens, x => Assert.NotNull(x.RevokedAtUtc));
        }
        Assert.Equal(HttpStatusCode.OK, (await admin.Client.PatchAsJsonAsync($"/api/admin/users/{target.UserId}/status", new { isActive = true })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(target.Username, "Secure123!")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(target.Cookie)).StatusCode);
        var self = await admin.Client.PatchAsJsonAsync($"/api/admin/users/{admin.UserId}/status", new { isActive = false });
        Assert.Equal(HttpStatusCode.Conflict, self.StatusCode);
        Assert.Contains("Kendi hesabınızı", await self.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.Client.PatchAsJsonAsync($"/api/admin/users/{target.UserId}/status", new { })).StatusCode);
    }

    [Fact]
    public async Task PasswordResetChangesHashRevokesRefreshAndReturnsNoContent()
    {
        var admin = await SessionAsync("passwordadmin", UserRole.Admin);
        var target = await SessionAsync("passwordtarget", UserRole.User);
        var before = await PasswordHashAsync(target.UserId);
        var response = await admin.Client.PutAsJsonAsync($"/api/admin/users/{target.UserId}/password",
            new { password = "Changed123!", passwordConfirmation = "Changed123!" });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode); Assert.Equal(0, response.Content.Headers.ContentLength ?? 0);
        Assert.NotEqual(before, await PasswordHashAsync(target.UserId));
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(target.Username, "Secure123!")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(target.Username, "Changed123!")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(target.Cookie)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await target.Client.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.Client.PutAsJsonAsync($"/api/admin/users/{Guid.NewGuid()}/password",
            new { password = "Changed123!", passwordConfirmation = "Changed123!" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.Client.PutAsJsonAsync($"/api/admin/users/{target.UserId}/password",
            new { password = "weak", passwordConfirmation = "different" })).StatusCode);
    }

    [Fact]
    public async Task CompactLogsNeverExposeCompleteOrFailedOrExpiredContent()
    {
        var admin = await SessionAsync("logadmin", UserRole.Admin);
        var source = "PRIVATE-SOURCE-" + string.Concat(Enumerable.Repeat("🙂e\u0301 line\n", 40)) + "END-SOURCE";
        await admin.Client.PostAsJsonAsync("/api/summaries", new { text = source, language = "Turkish" });
        await admin.Client.PostAsJsonAsync("/api/summaries", new { text = "provider-fail", language = "English" });
        const string expiredSource = "EXPIRED-COMPLETE-SOURCE"; const string expiredSummary = "EXPIRED-COMPLETE-SUMMARY";
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            db.SummaryRecords.Add(SummaryRecord.Create(admin.UserId, expiredSource, expiredSummary,
                SummaryLanguage.English, SummaryStatus.Succeeded, "Test", "expired-model", "summary-v1", 9,
                DateTimeOffset.UtcNow.AddDays(-31)));
            await db.SaveChangesAsync();
        }
        var response = await admin.Client.GetAsync($"/api/admin/logs?user={admin.Username}&page=1&pageSize=20");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); var json = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(source, json); Assert.DoesNotContain("Test özeti\"", json);
        Assert.DoesNotContain(expiredSource, json); Assert.DoesNotContain(expiredSummary, json);
        var page = JsonDocument.Parse(json).RootElement;
        var rows = page.GetProperty("items").EnumerateArray().ToArray();
        var failed = rows.Single(x => x.GetProperty("status").GetString() == "Failed");
        Assert.Equal(JsonValueKind.Null, failed.GetProperty("inputPreview").ValueKind);
        Assert.Equal(JsonValueKind.Null, failed.GetProperty("summaryPreview").ValueKind);
        Assert.Equal(AdminService.FailedPrivacyExplanation, failed.GetProperty("privacyExplanation").GetString());
        var expired = rows.Single(x => x.GetProperty("model").GetString() == "expired-model");
        Assert.Equal(JsonValueKind.Null, expired.GetProperty("inputPreview").ValueKind);
        var successful = rows.First(x => x.GetProperty("status").GetString() == "Succeeded" && x.GetProperty("model").GetString() != "expired-model");
        Assert.EndsWith("…", successful.GetProperty("inputPreview").GetString());
        Assert.True(new System.Globalization.StringInfo(successful.GetProperty("inputPreview").GetString()!).LengthInTextElements <= AdminService.PreviewLength);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.Client.GetAsync("/api/admin/logs?status=Unknown")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.Client.GetAsync("/api/admin/logs?fromUtc=2026-08-02T00:00:00Z&toUtc=2026-08-01T00:00:00Z")).StatusCode);
    }

    [Fact]
    public async Task PromptInfoIsActualReadOnlyMetadata()
    {
        var admin = await SessionAsync("promptadmin", UserRole.Admin);
        var response = await admin.Client.GetAsync("/api/admin/prompt-info");
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(SummarizationPromptBuilder.PromptVersion, json.GetProperty("version").GetString());
        Assert.False(json.GetProperty("editable").GetBoolean());
        Assert.False(json.TryGetProperty("systemInstruction", out _));
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await admin.Client.PostAsJsonAsync("/api/admin/prompt-info", new { version = "evil" })).StatusCode);
    }

    [Fact]
    public async Task StatisticsUseSevenUtcDaysAndDatabaseAggregates()
    {
        var admin = await SessionAsync("statsadmin", UserRole.Admin);
        var other = await SessionAsync("statsuser", UserRole.User);
        var today = DateTimeOffset.UtcNow.UtcDateTime.Date; var measuredDay = today.AddDays(-5);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            db.SummaryRecords.AddRange(
                SummaryRecord.Create(admin.UserId, "one", "summary", SummaryLanguage.Turkish, SummaryStatus.Succeeded, "StatsProvider", "stats-model", "summary-v1", 10, new(measuredDay.AddHours(1), TimeSpan.Zero)),
                SummaryRecord.Create(admin.UserId, "", null, SummaryLanguage.English, SummaryStatus.Failed, "StatsProvider", "stats-model", "summary-v1", 20, new(measuredDay.AddHours(2), TimeSpan.Zero), "Unavailable"),
                SummaryRecord.Create(other.UserId, "three", "summary", SummaryLanguage.Turkish, SummaryStatus.Succeeded, "StatsProvider", "stats-model", "summary-v1", 30, new(measuredDay.AddHours(3), TimeSpan.Zero)),
                SummaryRecord.Create(admin.UserId, "outside", "summary", SummaryLanguage.Turkish, SummaryStatus.Succeeded, "OutsideProvider", "outside", "summary-v1", 30, new(today.AddDays(-7), TimeSpan.Zero)));
            await db.SaveChangesAsync();
        }
        var after = await StatisticsAsync(admin.Client);
        Assert.Equal(7, after.GetProperty("days").GetArrayLength());
        var day = after.GetProperty("days").EnumerateArray().Single(x => x.GetProperty("dateUtc").GetString() == DateOnly.FromDateTime(measuredDay).ToString("yyyy-MM-dd"));
        Assert.Equal(3, day.GetProperty("total").GetInt32()); Assert.Equal(2, day.GetProperty("succeeded").GetInt32());
        Assert.Equal(1, day.GetProperty("failed").GetInt32()); Assert.Equal(66.67m, day.GetProperty("successRate").GetDecimal());
        Assert.Equal(2, day.GetProperty("turkish").GetInt32()); Assert.Equal(1, day.GetProperty("english").GetInt32());
        Assert.Equal(20, day.GetProperty("averageDurationMilliseconds").GetInt64()); Assert.Equal(2, day.GetProperty("activeUsers").GetInt32());
        Assert.True(after.GetProperty("activeUsers").GetInt32() >= 2);
        Assert.Equal(7, (DateTimeOffset.Parse(after.GetProperty("toExclusiveUtc").GetString()!) - DateTimeOffset.Parse(after.GetProperty("fromUtc").GetString()!)).TotalDays);
        Assert.Contains(after.GetProperty("providers").EnumerateArray(), x => x.GetProperty("provider").GetString() == "StatsProvider" && x.GetProperty("total").GetInt32() == 3);
        Assert.DoesNotContain("outside", after.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("summaryText", after.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("inputText", after.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConcurrentDeactivationCannotRemoveEveryActiveAdmin()
    {
        var first = await SessionAsync("raceadmina", UserRole.Admin);
        var second = await SessionAsync("raceadminb", UserRole.Admin);
        List<Guid> changed;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            var others = await db.Users.Where(x => x.Role == UserRole.Admin && x.IsActive && x.Id != first.UserId && x.Id != second.UserId).ToListAsync();
            changed = others.Select(x => x.Id).ToList(); foreach (var other in others) other.Deactivate(DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        }
        try
        {
            var responses = await Task.WhenAll(
                first.Client.PatchAsJsonAsync($"/api/admin/users/{second.UserId}/status", new { isActive = false }),
                second.Client.PatchAsJsonAsync($"/api/admin/users/{first.UserId}/status", new { isActive = false }));
            Assert.Contains(responses, x => x.StatusCode == HttpStatusCode.OK);
            Assert.Contains(responses, x => x.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.Forbidden);
            using var scope = factory.Services.CreateScope();
            Assert.True(await scope.ServiceProvider.GetRequiredService<AuthDbContext>().Users.CountAsync(x => x.Role == UserRole.Admin && x.IsActive) >= 1);
        }
        finally
        {
            using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            var restore = await db.Users.Where(x => changed.Contains(x.Id) || x.Id == first.UserId || x.Id == second.UserId).ToListAsync();
            foreach (var user in restore) user.Activate(DateTimeOffset.UtcNow); await db.SaveChangesAsync();
        }
    }

    private async Task<SessionData> SessionAsync(string prefix, UserRole role)
    {
        var username = prefix + Guid.NewGuid().ToString("N")[..8]; const string password = "Secure123!";
        var client = factory.Client(false); await client.PostAsJsonAsync("/api/auth/register", new { username, firstName = "Test", lastName = "User", password, passwordConfirmation = password });
        if (role == UserRole.Admin)
        {
            using var scope = factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<AuthDbContext>().Database.ExecuteSqlInterpolatedAsync($"UPDATE users SET role = 'Admin' WHERE normalized_username = {username.ToUpperInvariant()}");
        }
        var login = await client.PostAsJsonAsync("/api/auth/login", new { username, password });
        var access = JsonDocument.Parse(await login.Content.ReadAsStringAsync()).RootElement.GetProperty("accessToken").GetString()!;
        var cookie = login.Headers.GetValues("Set-Cookie").Single(x => x.StartsWith("refreshToken=", StringComparison.Ordinal)).Split(';')[0];
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", access);
        using var idScope = factory.Services.CreateScope();
        var id = await idScope.ServiceProvider.GetRequiredService<AuthDbContext>().Users.Where(x => x.NormalizedUsername == username.ToUpperInvariant()).Select(x => x.Id).SingleAsync();
        return new(client, id, username, cookie);
    }

    private Task<HttpResponseMessage> LoginAsync(string username, string password) =>
        factory.Client(false).PostAsJsonAsync("/api/auth/login", new { username, password });
    private Task<HttpResponseMessage> RefreshAsync(string cookie)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        request.Headers.TryAddWithoutValidation("Cookie", cookie); request.Headers.TryAddWithoutValidation("Origin", "https://spa.test");
        return factory.Client(false).SendAsync(request);
    }
    private async Task<string> PasswordHashAsync(Guid id)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AuthDbContext>().Users.Where(x => x.Id == id).Select(x => x.PasswordHash).SingleAsync();
    }
    private static async Task<JsonElement> StatisticsAsync(HttpClient client) =>
        JsonDocument.Parse(await (await client.GetAsync("/api/admin/statistics/seven-days")).Content.ReadAsStringAsync()).RootElement.Clone();
    private static HttpRequestMessage Request(HttpMethod method, string path, object? body)
    { var request = new HttpRequestMessage(method, path); if (body is not null) request.Content = JsonContent.Create(body); return request; }
    private static AdminCreateUserRequestData NewUser(string username, string role) =>
        new(username, "Ada", "Lovelace", "Secure123!", "Secure123!", role);
    private sealed record AdminCreateUserRequestData(string Username, string FirstName, string LastName,
        string Password, string PasswordConfirmation, string Role);
    private sealed record SessionData(HttpClient Client, Guid UserId, string Username, string Cookie);
    private sealed record UserItem(Guid Id, string Username, string FirstName, string LastName, string Role,
        bool IsActive, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);
    private sealed record UserList(UserItem[] Items, int Total, int Page, int PageSize);
}

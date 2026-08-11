using IFS.AIWeb.Application;
using IFS.AIWeb.Domain;

namespace IFS.AIWeb.Application.Tests;

public sealed class AuthServiceTests
{
    [Fact] public void Username_TrimsNormalizesAndComparesCaseInsensitively()
    { var lower = AuthValidation.Username("  öZkac_1 "); var upper = AuthValidation.Username("ÖZKAC_1"); Assert.Equal("öZkac_1", lower.Display); Assert.Equal(upper.Normalized, lower.Normalized); }
    [Theory] [InlineData("ab")] [InlineData("has space")] [InlineData("bad!")]
    public void Username_RejectsInvalidValues(string value) => Assert.Throws<RequestValidationException>(() => AuthValidation.Username(value));
    [Theory]
    [InlineData("Aa1!aaa")]
    [InlineData("aaaaaaaa1!")]
    [InlineData("AAAAAAAA1!")]
    [InlineData("Aaaaaaaa!")]
    [InlineData("Aaaaaaaa1")]
    public void Password_RejectsMissingPolicyRequirement(string value) => Assert.Throws<RequestValidationException>(() => AuthValidation.Password(value, value));
    [Fact] public void Password_AcceptsEightAndOneHundredTwentyEightCharacters()
    { AuthValidation.Password("Aa1!aaaa", "Aa1!aaaa"); var maximum = "Aa1!" + new string('a', 124); AuthValidation.Password(maximum, maximum); }
    [Fact] public void Password_RejectsOneHundredTwentyNineCharacters()
    { var value = "Aa1!" + new string('a', 125); Assert.Throws<RequestValidationException>(() => AuthValidation.Password(value, value)); }
    [Fact] public void Password_RejectsConfirmationMismatch() => Assert.Throws<RequestValidationException>(() => AuthValidation.Password("Valid123!", "Different123!"));
    [Fact] public async Task Register_AlwaysCreatesUserAndRejectsDuplicate()
    { var fixture = new Fixture(); var created = await fixture.Service.RegisterAsync(new("Member", "Ada", "Lovelace", "Secure123!", "Secure123!"), default); Assert.Equal("User", created.Role); Assert.True(fixture.Users.Items.Single().IsActive); await Assert.ThrowsAsync<UsernameConflictException>(() => fixture.Service.RegisterAsync(new("member", "New", "Name", "Another123!", "Another123!"), default)); }
    [Fact] public async Task Login_UsesSameGenericFailureForMissingWrongAndInactive()
    { var fixture = new Fixture(); await Assert.ThrowsAsync<AuthenticationFailedException>(() => fixture.Service.LoginAsync(new("missing", "wrong password"), default)); await Assert.ThrowsAsync<AuthenticationFailedException>(() => fixture.Service.LoginAsync(new("missing", null!), default)); await fixture.Service.RegisterAsync(new("member", "Ada", "Lovelace", "Secure123!", "Secure123!"), default); await Assert.ThrowsAsync<AuthenticationFailedException>(() => fixture.Service.LoginAsync(new("member", "wrong password"), default)); fixture.Users.Items.Single().Deactivate(fixture.Clock.UtcNow); await Assert.ThrowsAsync<AccountInactiveException>(() => fixture.Service.LoginAsync(new("member", "Secure123!"), default)); }
    [Fact] public async Task Refresh_RotatesAndReuseRevokesFamily()
    { var f = new Fixture(); await f.Service.RegisterAsync(new("member", "Ada", "Lovelace", "Secure123!", "Secure123!"), default); var login = await f.Service.LoginAsync(new("member", "Secure123!"), default); var refreshed = await f.Service.RefreshAsync(login.RefreshToken, default); Assert.NotEqual(login.RefreshToken, refreshed.RefreshToken); await Assert.ThrowsAsync<AuthenticationFailedException>(() => f.Service.RefreshAsync(login.RefreshToken, default)); Assert.All(f.Tokens.Items, x => Assert.NotNull(x.RevokedAtUtc)); }
    [Fact] public async Task Refresh_RejectsExpiredAndInactiveUsers()
    { var f = new Fixture(); await f.Service.RegisterAsync(new("member", "Ada", "Lovelace", "Secure123!", "Secure123!"), default); var login = await f.Service.LoginAsync(new("member", "Secure123!"), default); f.Clock.Now = f.Clock.Now.AddDays(8); await Assert.ThrowsAsync<AuthenticationFailedException>(() => f.Service.RefreshAsync(login.RefreshToken, default)); var f2 = new Fixture(); await f2.Service.RegisterAsync(new("member", "Ada", "Lovelace", "Secure123!", "Secure123!"), default); var login2 = await f2.Service.LoginAsync(new("member", "Secure123!"), default); f2.Users.Items.Single().Deactivate(f2.Clock.UtcNow); await Assert.ThrowsAsync<AuthenticationFailedException>(() => f2.Service.RefreshAsync(login2.RefreshToken, default)); }

    [Fact] public async Task Refresh_RejectsUnknownAndRevokedTokens()
    { var f = new Fixture(); await Assert.ThrowsAsync<AuthenticationFailedException>(() => f.Service.RefreshAsync("unknown", default)); await f.Service.RegisterAsync(new("member", "Ada", "Lovelace", "Secure123!", "Secure123!"), default); var login = await f.Service.LoginAsync(new("member", "Secure123!"), default); f.Tokens.Items.Single().Revoke(f.Clock.UtcNow, "test"); await Assert.ThrowsAsync<AuthenticationFailedException>(() => f.Service.RefreshAsync(login.RefreshToken, default)); Assert.All(f.Tokens.Items, token => Assert.NotNull(token.RevokedAtUtc)); }

    private sealed class Fixture
    {
        public Users Users { get; } = new(); public Tokens Tokens { get; } = new(); public Clock Clock { get; } = new(); public AuthService Service { get; }
        public Fixture() { Tokens.CurrentUsers = Users; var crypto = new Crypto(); Service = new(Users, Tokens, new Unit(), new Passwords(), new Access(Clock), crypto, Clock); }
    }
    private sealed class Users : IUserRepository { public List<User> Items { get; } = []; public Task<User?> FindByNormalizedUsernameAsync(string value, CancellationToken ct) => Task.FromResult(Items.SingleOrDefault(x => x.NormalizedUsername == value)); public Task<User?> FindByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(Items.SingleOrDefault(x => x.Id == id)); public void Add(User user) => Items.Add(user); }
    private sealed class Tokens : IRefreshTokenRepository { public List<RefreshToken> Items { get; } = []; public Task<RefreshToken?> FindByHashAsync(string hash, CancellationToken ct) => Task.FromResult(Items.SingleOrDefault(x => x.TokenHash == hash)); public Task<RefreshToken?> FindByHashForUpdateAsync(string hash, CancellationToken ct) => FindByHashAsync(hash, ct); public void Add(RefreshToken token) { var user = CurrentUsers?.Items.Single(x => x.Id == token.UserId); Items.Add(token); typeof(RefreshToken).GetProperty(nameof(RefreshToken.User))!.SetValue(token, user); } public Users? CurrentUsers { get; set; } public Task RevokeFamilyAsync(Guid family, DateTimeOffset now, string reason, CancellationToken ct) { foreach (var x in Items.Where(x => x.FamilyId == family)) x.Revoke(now, reason); return Task.CompletedTask; } }
    private sealed class Unit : IUnitOfWork { public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask; public Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct) => action(ct); }
    private sealed class Passwords : IPasswordService { public string Hash(User user, string password) => "hash:" + password; public bool Verify(User user, string hash, string password) => hash == "hash:" + password; }
    private sealed class Crypto : IRefreshTokenService { int i; public string Generate() => "token-" + ++i; public string Hash(string token) => "hash-" + token; }
    private sealed class Clock : IClock { public DateTimeOffset Now { get; set; } = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero); public DateTimeOffset UtcNow => Now; }
    private sealed class Access(Clock clock) : IAccessTokenService { public (string Token, DateTimeOffset ExpiresAtUtc) Create(User user) => ("access", clock.UtcNow.AddMinutes(15)); }
}

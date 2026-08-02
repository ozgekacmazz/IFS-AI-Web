using IFS.AIWeb.Domain;

namespace IFS.AIWeb.Application;

public sealed class AuthService(IUserRepository users, IRefreshTokenRepository tokens, IUnitOfWork unit,
    IPasswordService passwords, IAccessTokenService accessTokens, IRefreshTokenService refreshTokens, IClock clock)
{
    public async Task<SafeUser> RegisterAsync(RegisterCommand command, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        string display = string.Empty; string normalized = string.Empty; string first = string.Empty; string last = string.Empty;
        try { (display, normalized) = AuthValidation.Username(command.Username); } catch (RequestValidationException ex) { Merge(errors, ex.Errors); }
        try { first = AuthValidation.Name(command.FirstName, "firstName"); } catch (RequestValidationException ex) { Merge(errors, ex.Errors); }
        try { last = AuthValidation.Name(command.LastName, "lastName"); } catch (RequestValidationException ex) { Merge(errors, ex.Errors); }
        Merge(errors, AuthValidation.PasswordErrors(command.Password, command.PasswordConfirmation));
        if (errors.Count > 0) throw new RequestValidationException(errors);
        if (await users.FindByNormalizedUsernameAsync(normalized, ct) is not null) throw new UsernameConflictException();
        var now = clock.UtcNow;
        var user = User.Create(Guid.NewGuid(), display, normalized, first, last, string.Empty, UserRole.User, now);
        var hash = passwords.Hash(user, command.Password);
        user = User.Create(user.Id, display, normalized, first, last, hash, UserRole.User, now);
        users.Add(user); await unit.SaveChangesAsync(ct); return Safe(user);
    }

    public async Task<AuthResult> LoginAsync(LoginCommand command, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(command.Password)) throw new AuthenticationFailedException();
        string normalized;
        try { normalized = AuthValidation.Username(command.Username).Normalized; } catch (RequestValidationException) { throw new AuthenticationFailedException(); }
        var user = await users.FindByNormalizedUsernameAsync(normalized, ct);
        if (user is null || !user.IsActive || !passwords.Verify(user, user.PasswordHash, command.Password)) throw new AuthenticationFailedException();
        var refresh = NewRefresh(user.Id, Guid.NewGuid()); tokens.Add(refresh.Entity); await unit.SaveChangesAsync(ct);
        var access = accessTokens.Create(user); return new(access.Token, access.ExpiresAtUtc, Safe(user), refresh.Plaintext, refresh.Entity.ExpiresAtUtc);
    }

    public Task<RefreshResult> RefreshAsync(string plaintext, CancellationToken ct) => unit.InTransactionAsync(async inner =>
    {
        var now = clock.UtcNow; var current = await tokens.FindByHashForUpdateAsync(refreshTokens.Hash(plaintext), inner);
        if (current is null) throw new AuthenticationFailedException();
        if (current.RevokedAtUtc is not null) { await tokens.RevokeFamilyAsync(current.FamilyId, now, "Tekrar kullanım tespit edildi", inner); await unit.SaveChangesAsync(inner); throw new AuthenticationFailedException(); }
        if (!current.IsActive(now) || !current.User.IsActive) { await tokens.RevokeFamilyAsync(current.FamilyId, now, "Süresi doldu veya kullanıcı etkin değil", inner); await unit.SaveChangesAsync(inner); throw new AuthenticationFailedException(); }
        var next = NewRefresh(current.UserId, current.FamilyId); current.Revoke(now, "Döndürüldü", next.Entity.Id); tokens.Add(next.Entity); await unit.SaveChangesAsync(inner);
        var access = accessTokens.Create(current.User); return new RefreshResult(access.Token, access.ExpiresAtUtc, Safe(current.User), next.Plaintext, next.Entity.ExpiresAtUtc);
    }, ct);

    public async Task LogoutAsync(string? plaintext, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(plaintext)) return;
        var token = await tokens.FindByHashAsync(refreshTokens.Hash(plaintext), ct);
        if (token?.IsActive(clock.UtcNow) == true) { token.Revoke(clock.UtcNow, "Çıkış yapıldı"); await unit.SaveChangesAsync(ct); }
    }
    public async Task<SafeUser> GetUserAsync(Guid id, CancellationToken ct)
    { var user = await users.FindByIdAsync(id, ct); if (user is null || !user.IsActive) throw new AuthenticationFailedException(); return Safe(user); }
    private (string Plaintext, RefreshToken Entity) NewRefresh(Guid userId, Guid family)
    { var plain = refreshTokens.Generate(); var now = clock.UtcNow; return (plain, RefreshToken.Create(Guid.NewGuid(), userId, refreshTokens.Hash(plain), family, now, now.AddDays(7))); }
    private static SafeUser Safe(User user) => new(user.Username, user.FirstName, user.LastName, user.Role.ToString());
    private static void Merge(Dictionary<string, string[]> target, Dictionary<string, string[]> source)
    { foreach (var (key, messages) in source) target[key] = target.TryGetValue(key, out var existing) ? [.. existing, .. messages] : messages; }
}

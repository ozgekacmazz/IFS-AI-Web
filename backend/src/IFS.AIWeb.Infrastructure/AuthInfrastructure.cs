using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using IFS.AIWeb.Application;
using IFS.AIWeb.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

namespace IFS.AIWeb.Infrastructure;

internal sealed class UserRepository(AuthDbContext db) : IUserRepository
{
    public Task<User?> FindByNormalizedUsernameAsync(string value, CancellationToken ct) => db.Users.SingleOrDefaultAsync(x => x.NormalizedUsername == value, ct);
    public Task<User?> FindByIdAsync(Guid id, CancellationToken ct) => db.Users.SingleOrDefaultAsync(x => x.Id == id, ct);
    public void Add(User user) => db.Users.Add(user);
}
internal sealed class RefreshTokenRepository(AuthDbContext db) : IRefreshTokenRepository
{
    public Task<RefreshToken?> FindByHashAsync(string hash, CancellationToken ct) => db.RefreshTokens.Include(x => x.User).SingleOrDefaultAsync(x => x.TokenHash == hash, ct);
    public Task<RefreshToken?> FindByHashForUpdateAsync(string hash, CancellationToken ct) => db.RefreshTokens
        .FromSqlInterpolated($"SELECT * FROM refresh_tokens WHERE token_hash = {hash} FOR UPDATE")
        .Include(x => x.User).SingleOrDefaultAsync(ct);
    public void Add(RefreshToken token) => db.RefreshTokens.Add(token);
    public async Task RevokeFamilyAsync(Guid familyId, DateTimeOffset now, string reason, CancellationToken ct)
    { var active = await db.RefreshTokens.Where(x => x.FamilyId == familyId && x.RevokedAtUtc == null).ToListAsync(ct); foreach (var token in active) token.Revoke(now, reason); }
}
internal sealed class UnitOfWork(AuthDbContext db) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken ct)
    { try { await db.SaveChangesAsync(ct); } catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "ux_users_normalized_username" }) { throw new UsernameConflictException(); } }
    public async Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct)
    { await using var tx = await db.Database.BeginTransactionAsync(ct); try { var result = await action(ct); await tx.CommitAsync(ct); return result; } catch (AuthenticationFailedException) { await tx.CommitAsync(ct); throw; } }
}
internal sealed class PasswordService : IPasswordService
{
    private readonly PasswordHasher<User> hasher = new();
    public string Hash(User user, string password) => hasher.HashPassword(user, password);
    public bool Verify(User user, string hash, string password) => hasher.VerifyHashedPassword(user, hash, password) != PasswordVerificationResult.Failed;
}
internal sealed class RefreshTokenService : IRefreshTokenService
{
    public string Generate() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    public string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
internal sealed class SystemClock : IClock { public DateTimeOffset UtcNow => DateTimeOffset.UtcNow; }
public sealed class JwtOptions { public string Issuer { get; set; } = ""; public string Audience { get; set; } = ""; public string SigningKey { get; set; } = ""; public int AccessMinutes { get; set; } = 15; }
internal sealed class AccessTokenService(JwtOptions options, IClock clock) : IAccessTokenService
{
    public (string Token, DateTimeOffset ExpiresAtUtc) Create(User user)
    {
        var expires = clock.UtcNow.AddMinutes(options.AccessMinutes); var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(options.Issuer, options.Audience, [new(JwtRegisteredClaimNames.Sub, user.Id.ToString()), new(ClaimTypes.Role, user.Role.ToString()), new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())], clock.UtcNow.UtcDateTime, expires.UtcDateTime, credentials);
        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}

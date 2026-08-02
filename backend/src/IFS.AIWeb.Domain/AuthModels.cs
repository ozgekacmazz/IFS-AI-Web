namespace IFS.AIWeb.Domain;

public enum UserRole { User, Admin }

public sealed class User
{
    private User() { }
    private User(Guid id, string username, string normalizedUsername, string firstName, string lastName,
        string passwordHash, UserRole role, DateTimeOffset now)
    {
        Id = id; Username = username; NormalizedUsername = normalizedUsername; FirstName = firstName;
        LastName = lastName; PasswordHash = passwordHash; Role = role; IsActive = true;
        CreatedAtUtc = now; UpdatedAtUtc = now;
    }

    public Guid Id { get; private set; }
    public string Username { get; private set; } = string.Empty;
    public string NormalizedUsername { get; private set; } = string.Empty;
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public UserRole Role { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static User Create(Guid id, string username, string normalizedUsername, string firstName,
        string lastName, string passwordHash, UserRole role, DateTimeOffset now) =>
        new(id, username, normalizedUsername, firstName, lastName, passwordHash, role, now);

    public void Deactivate(DateTimeOffset now) { IsActive = false; UpdatedAtUtc = now; }
}

public sealed class RefreshToken
{
    private RefreshToken() { }
    private RefreshToken(Guid id, Guid userId, string tokenHash, Guid familyId, DateTimeOffset created, DateTimeOffset expires)
    { Id = id; UserId = userId; TokenHash = tokenHash; FamilyId = familyId; CreatedAtUtc = created; ExpiresAtUtc = expires; }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public Guid FamilyId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset? RevokedAtUtc { get; private set; }
    public Guid? ReplacedByTokenId { get; private set; }
    public string? RevocationReason { get; private set; }
    public User User { get; private set; } = null!;
    public bool IsActive(DateTimeOffset now) => RevokedAtUtc is null && ExpiresAtUtc > now;
    public static RefreshToken Create(Guid id, Guid userId, string hash, Guid familyId, DateTimeOffset now, DateTimeOffset expires) =>
        new(id, userId, hash, familyId, now, expires);
    public void Revoke(DateTimeOffset now, string reason, Guid? replacement = null)
    { if (RevokedAtUtc is null) { RevokedAtUtc = now; RevocationReason = reason; ReplacedByTokenId = replacement; } }
}

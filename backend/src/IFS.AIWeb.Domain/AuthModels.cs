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

    public void Activate(DateTimeOffset now) { IsActive = true; UpdatedAtUtc = now; }
    public void Deactivate(DateTimeOffset now) { IsActive = false; UpdatedAtUtc = now; }
    public void ChangePasswordHash(string passwordHash, DateTimeOffset now)
    { PasswordHash = passwordHash; UpdatedAtUtc = now; }
}

public sealed class RefreshToken
{
    private RefreshToken() { }
    private RefreshToken(Guid id, Guid userId, string tokenHash, Guid familyId, DateTimeOffset created, DateTimeOffset expires, DateTimeOffset absoluteExpires)
    { Id = id; UserId = userId; TokenHash = tokenHash; FamilyId = familyId; CreatedAtUtc = created; ExpiresAtUtc = expires; AbsoluteExpiresAtUtc = absoluteExpires; }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public Guid FamilyId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset AbsoluteExpiresAtUtc { get; private set; }
    public DateTimeOffset? RevokedAtUtc { get; private set; }
    public Guid? ReplacedByTokenId { get; private set; }
    public string? RevocationReason { get; private set; }
    public User User { get; private set; } = null!;
    public bool IsActive(DateTimeOffset now) => RevokedAtUtc is null && ExpiresAtUtc > now;
    public static RefreshToken Create(Guid id, Guid userId, string hash, Guid familyId, DateTimeOffset now, DateTimeOffset expires, DateTimeOffset absoluteExpires) =>
        new(id, userId, hash, familyId, now, expires, absoluteExpires);
    public void Revoke(DateTimeOffset now, string reason, Guid? replacement = null)
    { if (RevokedAtUtc is null) { RevokedAtUtc = now; RevocationReason = reason; ReplacedByTokenId = replacement; } }
}

public enum SummaryLanguage { Turkish, English }
public enum SummaryStatus { Succeeded, Failed }
public enum SummaryFeedback { Useful, NotUseful }

public sealed class SummaryRecord
{
    private SummaryRecord() { }
    private SummaryRecord(Guid id, Guid userId, string input, string? summary, SummaryLanguage language,
        SummaryStatus status, string provider, string model, string promptVersion, int inputCount,
        int outputCount, long durationMs, DateTimeOffset created, string? failureCode)
    { Id = id; UserId = userId; InputText = input; SummaryText = summary; RequestedLanguage = language;
      Status = status; Provider = provider; Model = model; PromptVersion = promptVersion;
      InputCharacterCount = inputCount; OutputCharacterCount = outputCount; DurationMilliseconds = durationMs;
      CreatedAtUtc = created; ExpiresAtUtc = created.AddDays(30); FailureCode = failureCode; }
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string InputText { get; private set; } = string.Empty;
    public string? SummaryText { get; private set; }
    public SummaryLanguage RequestedLanguage { get; private set; }
    public SummaryStatus Status { get; private set; }
    public string Provider { get; private set; } = string.Empty;
    public string Model { get; private set; } = string.Empty;
    public string PromptVersion { get; private set; } = string.Empty;
    public int InputCharacterCount { get; private set; }
    public int OutputCharacterCount { get; private set; }
    public long DurationMilliseconds { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public string? FailureCode { get; private set; }
    public SummaryFeedback? Feedback { get; private set; }
    public DateTimeOffset? FeedbackUpdatedAtUtc { get; private set; }
    public User User { get; private set; } = null!;
    public static SummaryRecord Create(Guid userId, string input, string? summary, SummaryLanguage language,
        SummaryStatus status, string provider, string model, string promptVersion, long durationMs,
        DateTimeOffset now, string? failureCode = null) => new(Guid.NewGuid(), userId, input, summary, language,
        status, provider, model, promptVersion, input.Length, summary?.Length ?? 0, durationMs, now, failureCode);
    public void SetFeedback(SummaryFeedback feedback, DateTimeOffset now)
    {
        if (Feedback == feedback) return;
        Feedback = feedback; FeedbackUpdatedAtUtc = now;
    }
}

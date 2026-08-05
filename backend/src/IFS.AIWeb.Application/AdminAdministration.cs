using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using IFS.AIWeb.Domain;

namespace IFS.AIWeb.Application;

public sealed record AdminUserDto(Guid Id, string Username, string FirstName, string LastName, string Role,
    bool IsActive, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);
public sealed record AdminUserListDto(IReadOnlyList<AdminUserDto> Items, int Total, int Page, int PageSize);
public sealed record AdminUserListQuery(int Page = 1, int PageSize = 20, string? Search = null,
    string? Role = null, bool? IsActive = null);
public sealed record AdminCreateUserCommand(string Username, string FirstName, string LastName, string Password,
    string PasswordConfirmation, string Role);
public sealed record AdminSetUserStatusCommand(Guid ActorUserId, Guid TargetUserId, bool IsActive);
public sealed record AdminResetPasswordCommand(Guid TargetUserId, string Password, string PasswordConfirmation);

public enum AdminUserMutationResult { Updated, NotFound, SelfDeactivation, LastActiveAdmin }
public sealed record AdminUserPage(IReadOnlyList<User> Items, int Total);

public sealed record AdminLogQuery(int Page = 1, int PageSize = 20, string? Status = null,
    string? Language = null, string? User = null, DateTimeOffset? FromUtc = null, DateTimeOffset? ToUtc = null);
public sealed record AdminLogProjection(Guid Id, DateTimeOffset CreatedAtUtc, DateTimeOffset ExpiresAtUtc,
    string Username, SummaryStatus Status, SummaryLanguage Language, string Provider, string Model,
    string PromptVersion, long DurationMilliseconds, int InputCharacterCount, int OutputCharacterCount,
    string? FailureCode, string InputPrefix, string? SummaryPrefix);
public sealed record AdminLogDto(Guid Id, DateTimeOffset CreatedAtUtc, string Username, string Status,
    string Language, string Provider, string Model, string PromptVersion, long DurationMilliseconds,
    int InputCharacterCount, int OutputCharacterCount, string? FailureCategory, string? InputPreview,
    string? SummaryPreview, string? PrivacyExplanation);
public sealed record AdminLogPage(IReadOnlyList<AdminLogProjection> Items, int Total);
public sealed record AdminLogListDto(IReadOnlyList<AdminLogDto> Items, int Total, int Page, int PageSize);

public sealed record AdminStatisticsDayProjection(DateOnly DateUtc, int Total, int Succeeded, int Failed,
    int Turkish, int English, decimal AverageDurationMilliseconds, int ActiveUsers);
public sealed record AdminProviderStatistics(string Provider, string Model, int Total);
public sealed record AdminFeedbackSummary(int Useful, int NotUseful, decimal SatisfactionRate);
public sealed record AdminStatisticsData(IReadOnlyList<AdminStatisticsDayProjection> Days,
    IReadOnlyList<AdminProviderStatistics> Providers, int ActiveUsers, AdminFeedbackSummary Feedback);
public sealed record AdminStatisticsDay(DateOnly DateUtc, int Total, int Succeeded, int Failed,
    decimal SuccessRate, int Turkish, int English, long AverageDurationMilliseconds, int ActiveUsers);
public sealed record AdminSevenDayStatistics(DateTimeOffset FromUtc, DateTimeOffset ToExclusiveUtc,
    IReadOnlyList<AdminStatisticsDay> Days, IReadOnlyList<AdminProviderStatistics> Providers, int ActiveUsers,
    AdminFeedbackSummary Feedback);
public sealed record AdminPromptInfo(string Version, string Purpose, IReadOnlyList<string> SupportedLanguages, bool Editable);

public sealed class AdminUserNotFoundException : Exception;
public sealed class AdminSelfDeactivationException : Exception;
public sealed class AdminLastActiveException : Exception;

public interface IAdminRepository
{
    Task<AdminUserPage> GetUsersAsync(int page, int pageSize, string? normalizedSearch, UserRole? role,
        bool? isActive, CancellationToken ct);
    Task<User?> FindTrackedUserAsync(Guid id, CancellationToken ct);
    Task<AdminUserMutationResult> SetUserStatusAsync(Guid actorId, Guid targetId, bool isActive,
        DateTimeOffset now, CancellationToken ct);
    Task<bool> ResetPasswordAsync(Guid targetId, string passwordHash, DateTimeOffset now, CancellationToken ct);
    Task<AdminLogPage> GetLogsAsync(int page, int pageSize, SummaryStatus? status, SummaryLanguage? language,
        string? normalizedUser, DateTimeOffset? fromUtc, DateTimeOffset? toUtc, int prefixLength, CancellationToken ct);
    Task<AdminStatisticsData> GetStatisticsAsync(DateTimeOffset fromUtc,
        DateTimeOffset toExclusiveUtc, CancellationToken ct);
}

public sealed class AdminService(IAdminRepository admin, IUserRepository users, IUnitOfWork unit,
    IPasswordService passwords, IClock clock)
{
    public const int PreviewLength = 160;
    private const int PreviewReadLength = 512;
    public const string FailedPrivacyExplanation =
        "Bu işlem başarısız olduğu için kaynak metin güvenlik ve mahremiyet amacıyla saklanmadı.";

    public async Task<AdminUserListDto> GetUsersAsync(AdminUserListQuery query, CancellationToken ct)
    {
        ValidatePage(query.Page, query.PageSize);
        UserRole? role = null;
        if (!string.IsNullOrWhiteSpace(query.Role))
        {
            if (!TryParseNamedEnum<UserRole>(query.Role, out var parsedRole))
                throw Validation("role", "Rol User veya Admin olmalıdır.");
            role = parsedRole;
        }
        var search = NormalizeSearch(query.Search);
        var page = await admin.GetUsersAsync(query.Page, query.PageSize, search, role, query.IsActive, ct);
        return new(page.Items.Select(MapUser).ToArray(), page.Total, query.Page, query.PageSize);
    }

    public async Task<AdminUserDto> CreateUserAsync(AdminCreateUserCommand command, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        string display = "", normalized = "", first = "", last = "";
        try { (display, normalized) = AuthValidation.Username(command.Username); } catch (RequestValidationException ex) { Merge(errors, ex.Errors); }
        try { first = AuthValidation.Name(command.FirstName, "firstName"); } catch (RequestValidationException ex) { Merge(errors, ex.Errors); }
        try { last = AuthValidation.Name(command.LastName, "lastName"); } catch (RequestValidationException ex) { Merge(errors, ex.Errors); }
        Merge(errors, AuthValidation.PasswordErrors(command.Password, command.PasswordConfirmation));
        if (!TryParseNamedEnum<UserRole>(command.Role, out var role)) errors["role"] = ["Rol User veya Admin olmalıdır."];
        if (errors.Count > 0) throw new RequestValidationException(errors);
        if (await users.FindByNormalizedUsernameAsync(normalized, ct) is not null) throw new UsernameConflictException();
        var now = clock.UtcNow;
        var shell = User.Create(Guid.NewGuid(), display, normalized, first, last, "", role, now);
        var user = User.Create(shell.Id, display, normalized, first, last, passwords.Hash(shell, command.Password), role, now);
        users.Add(user); await unit.SaveChangesAsync(ct);
        return MapUser(user);
    }

    public async Task<AdminUserDto> SetStatusAsync(AdminSetUserStatusCommand command, CancellationToken ct)
    {
        var result = await admin.SetUserStatusAsync(command.ActorUserId, command.TargetUserId,
            command.IsActive, clock.UtcNow, ct);
        if (result == AdminUserMutationResult.NotFound) throw new AdminUserNotFoundException();
        if (result == AdminUserMutationResult.SelfDeactivation) throw new AdminSelfDeactivationException();
        if (result == AdminUserMutationResult.LastActiveAdmin) throw new AdminLastActiveException();
        var updated = await users.FindByIdAsync(command.TargetUserId, ct) ?? throw new AdminUserNotFoundException();
        return MapUser(updated);
    }

    public async Task ResetPasswordAsync(AdminResetPasswordCommand command, CancellationToken ct)
    {
        AuthValidation.Password(command.Password, command.PasswordConfirmation);
        var user = await admin.FindTrackedUserAsync(command.TargetUserId, ct) ?? throw new AdminUserNotFoundException();
        var hash = passwords.Hash(user, command.Password);
        if (!await admin.ResetPasswordAsync(command.TargetUserId, hash, clock.UtcNow, ct))
            throw new AdminUserNotFoundException();
    }

    public async Task<AdminLogListDto> GetLogsAsync(AdminLogQuery query, CancellationToken ct)
    {
        ValidatePage(query.Page, query.PageSize);
        SummaryStatus? status = null; SummaryLanguage? language = null;
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (!TryParseNamedEnum<SummaryStatus>(query.Status, out var parsedStatus))
                throw Validation("status", "Durum Succeeded veya Failed olmalıdır.");
            status = parsedStatus;
        }
        if (!string.IsNullOrWhiteSpace(query.Language))
        {
            if (!TryParseNamedEnum<SummaryLanguage>(query.Language, out var parsedLanguage))
                throw Validation("language", "Dil Turkish veya English olmalıdır.");
            language = parsedLanguage;
        }
        if (query.FromUtc is not null && query.ToUtc is not null && query.FromUtc >= query.ToUtc)
            throw Validation("toUtc", "Bitiş zamanı başlangıç zamanından sonra olmalıdır.");
        var now = clock.UtcNow;
        var page = await admin.GetLogsAsync(query.Page, query.PageSize, status, language,
            NormalizeSearch(query.User), query.FromUtc, query.ToUtc, PreviewReadLength, ct);
        var items = page.Items.Select(x =>
        {
            var contentAvailable = x.Status == SummaryStatus.Succeeded && x.ExpiresAtUtc > now;
            return new AdminLogDto(x.Id, x.CreatedAtUtc, x.Username, x.Status.ToString(), x.Language.ToString(),
                x.Provider, x.Model, x.PromptVersion, x.DurationMilliseconds, x.InputCharacterCount,
                x.OutputCharacterCount, x.FailureCode, contentAvailable ? Preview(x.InputPrefix) : null,
                contentAvailable ? Preview(x.SummaryPrefix) : null,
                x.Status == SummaryStatus.Failed ? FailedPrivacyExplanation : null);
        }).ToArray();
        return new(items, page.Total, query.Page, query.PageSize);
    }

    public AdminPromptInfo GetPromptInfo() => new(SummarizationPromptBuilder.PromptVersion,
        "Kullanıcı tarafından sağlanan metni kaynakta bulunmayan bilgi eklemeden kısa ve anlaşılır biçimde özetler.",
        [SummaryLanguage.Turkish.ToString(), SummaryLanguage.English.ToString()], false);

    public async Task<AdminSevenDayStatistics> GetSevenDayStatisticsAsync(CancellationToken ct)
    {
        var today = new DateTimeOffset(clock.UtcNow.UtcDateTime.Date, TimeSpan.Zero);
        var from = today.AddDays(-6); var to = today.AddDays(1);
        var statistics = await admin.GetStatisticsAsync(from, to, ct);
        var days = Enumerable.Range(0, 7).Select(offset =>
        {
            var date = DateOnly.FromDateTime(from.AddDays(offset).UtcDateTime);
            var value = statistics.Days.SingleOrDefault(x => x.DateUtc == date);
            if (value is null) return new AdminStatisticsDay(date, 0, 0, 0, 0, 0, 0, 0, 0);
            return new AdminStatisticsDay(date, value.Total, value.Succeeded, value.Failed,
                value.Total == 0 ? 0 : Math.Round((decimal)value.Succeeded * 100 / value.Total, 2),
                value.Turkish, value.English,
                (long)Math.Round(value.AverageDurationMilliseconds, MidpointRounding.AwayFromZero), value.ActiveUsers);
        }).ToArray();
        return new(from, to, days, statistics.Providers, statistics.ActiveUsers, statistics.Feedback);
    }

    private static string? NormalizeSearch(string? value) => string.IsNullOrWhiteSpace(value)
        ? null : value.Trim().Normalize(NormalizationForm.FormKC).ToUpperInvariant();
    private static string Preview(string? value)
    {
        var compact = Regex.Replace(value ?? "", @"\s+", " ").Trim();
        var elements = StringInfo.ParseCombiningCharacters(compact);
        if (elements.Length == 0) return "";
        var take = Math.Min(PreviewLength - 1, elements.Length - 1);
        return take == 0 ? "…" : compact[..elements[take]] + "…";
    }
    private static void ValidatePage(int page, int pageSize)
    {
        var errors = new Dictionary<string, string[]>();
        if (page < 1) errors["page"] = ["Sayfa en az 1 olmalıdır."];
        if (pageSize is < 1 or > 100) errors["pageSize"] = ["Sayfa boyutu 1-100 arasında olmalıdır."];
        if (errors.Count > 0) throw new RequestValidationException(errors);
    }
    private static RequestValidationException Validation(string key, string value) => new(new() { [key] = [value] });
    private static bool TryParseNamedEnum<T>(string? value, out T parsed) where T : struct, Enum
    {
        if (value is not null && Enum.GetNames<T>().Any(name => name.Equals(value, StringComparison.OrdinalIgnoreCase)))
        { parsed = Enum.Parse<T>(value, true); return true; }
        parsed = default; return false;
    }
    private static AdminUserDto MapUser(User user) => new(user.Id, user.Username, user.FirstName, user.LastName,
        user.Role.ToString(), user.IsActive, user.CreatedAtUtc, user.UpdatedAtUtc);
    private static void Merge(Dictionary<string, string[]> target, Dictionary<string, string[]> source)
    { foreach (var (key, messages) in source) target[key] = target.TryGetValue(key, out var current) ? [.. current, .. messages] : messages; }
}

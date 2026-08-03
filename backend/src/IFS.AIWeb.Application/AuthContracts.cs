using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using IFS.AIWeb.Domain;

namespace IFS.AIWeb.Application;

public sealed record RegisterCommand(string Username, string FirstName, string LastName, string Password, string PasswordConfirmation);
public sealed record LoginCommand(string Username, string Password);
public sealed record SafeUser(string Username, string FirstName, string LastName, string Role);
public sealed record AuthResult(string AccessToken, DateTimeOffset AccessTokenExpiresAtUtc, SafeUser User, string RefreshToken, DateTimeOffset RefreshTokenExpiresAtUtc);
public sealed record RefreshResult(string AccessToken, DateTimeOffset AccessTokenExpiresAtUtc, SafeUser User, string RefreshToken, DateTimeOffset RefreshTokenExpiresAtUtc);

public sealed class RequestValidationException(Dictionary<string, string[]> errors) : Exception("İstek doğrulanamadı.") { public Dictionary<string, string[]> Errors { get; } = errors; }
public sealed class UsernameConflictException : Exception { }
public sealed class AuthenticationFailedException : Exception { }
public sealed class AccountInactiveException : Exception { }

public static partial class AuthValidation
{
    [GeneratedRegex(@"^[\p{L}\p{Nd}._-]+$", RegexOptions.CultureInvariant)] private static partial Regex UsernameRegex();
    [GeneratedRegex(@"^[\p{L}\p{M} '\-]+$", RegexOptions.CultureInvariant)] private static partial Regex NameRegex();
    public static (string Display, string Normalized) Username(string value)
    {
        var display = (value ?? string.Empty).Trim().Normalize(NormalizationForm.FormKC);
        if (display.Length is < 3 or > 32 || !UsernameRegex().IsMatch(display))
            throw Validation("username", "Kullanıcı adı 3-32 karakter olmalı; yalnızca harf, rakam, nokta, alt çizgi ve kısa çizgi içermelidir.");
        return (display, display.ToUpperInvariant());
    }
    public static string Name(string value, string field)
    {
        var result = (value ?? string.Empty).Trim().Normalize(NormalizationForm.FormKC);
        if (result.Length is < 1 or > 80 || !NameRegex().IsMatch(result))
            throw Validation(field, "Alan 1-80 karakter olmalı ve yalnızca adlarda kullanılan karakterleri içermelidir.");
        return result;
    }
    public static void Password(string password, string? confirmation = null)
    {
        var errors = PasswordErrors(password, confirmation);
        if (errors.Count > 0) throw new RequestValidationException(errors);
    }

    public static Dictionary<string, string[]> PasswordErrors(string? password, string? confirmation)
    {
        var passwordErrors = new List<string>();
        if (password is null)
            passwordErrors.Add("Şifre zorunludur.");
        else
        {
            var length = password.EnumerateRunes().Count();
            if (length < 8) passwordErrors.Add("Şifre en az 8 karakter olmalıdır.");
            if (length > 128) passwordErrors.Add("Şifre en fazla 128 karakter olmalıdır.");
            if (!password.EnumerateRunes().Any(r => Rune.GetUnicodeCategory(r) == UnicodeCategory.UppercaseLetter)) passwordErrors.Add("Şifre en az bir büyük harf içermelidir.");
            if (!password.EnumerateRunes().Any(r => Rune.GetUnicodeCategory(r) == UnicodeCategory.LowercaseLetter)) passwordErrors.Add("Şifre en az bir küçük harf içermelidir.");
            if (!password.EnumerateRunes().Any(r => Rune.GetUnicodeCategory(r) == UnicodeCategory.DecimalDigitNumber)) passwordErrors.Add("Şifre en az bir rakam içermelidir.");
            if (!password.EnumerateRunes().Any(IsSpecial)) passwordErrors.Add("Şifre en az bir noktalama veya özel karakter içermelidir.");
        }
        var errors = new Dictionary<string, string[]>();
        if (passwordErrors.Count > 0) errors["password"] = [.. passwordErrors];
        if (confirmation is null) errors["passwordConfirmation"] = ["Şifre tekrarı zorunludur."];
        else if (password is not null && password != confirmation) errors["passwordConfirmation"] = ["Şifreler eşleşmiyor."];
        return errors;
    }

    private static bool IsSpecial(Rune rune) => Rune.GetUnicodeCategory(rune) is
        UnicodeCategory.ConnectorPunctuation or UnicodeCategory.DashPunctuation or UnicodeCategory.OpenPunctuation or
        UnicodeCategory.ClosePunctuation or UnicodeCategory.InitialQuotePunctuation or UnicodeCategory.FinalQuotePunctuation or
        UnicodeCategory.OtherPunctuation or UnicodeCategory.MathSymbol or UnicodeCategory.CurrencySymbol or
        UnicodeCategory.ModifierSymbol or UnicodeCategory.OtherSymbol;
    private static RequestValidationException Validation(string key, string message) => new(new() { [key] = [message] });
}

public interface IUserRepository
{
    Task<User?> FindByNormalizedUsernameAsync(string normalized, CancellationToken ct);
    Task<User?> FindByIdAsync(Guid id, CancellationToken ct);
    void Add(User user);
}
public interface IRefreshTokenRepository
{
    Task<RefreshToken?> FindByHashAsync(string hash, CancellationToken ct);
    Task<RefreshToken?> FindByHashForUpdateAsync(string hash, CancellationToken ct);
    void Add(RefreshToken token);
    Task RevokeFamilyAsync(Guid familyId, DateTimeOffset now, string reason, CancellationToken ct);
}
public interface IUnitOfWork { Task SaveChangesAsync(CancellationToken ct); Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct); }
public interface IPasswordService { string Hash(User user, string password); bool Verify(User user, string hash, string password); }
public interface IAccessTokenService { (string Token, DateTimeOffset ExpiresAtUtc) Create(User user); }
public interface IRefreshTokenService { string Generate(); string Hash(string token); }
public interface IClock { DateTimeOffset UtcNow { get; } }

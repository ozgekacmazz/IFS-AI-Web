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
        if (password is null || password.Length is < 12 or > 128) throw Validation("password", "Şifre 12-128 karakter olmalıdır.");
        if (confirmation is not null && password != confirmation) throw Validation("passwordConfirmation", "Şifreler eşleşmiyor.");
    }
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

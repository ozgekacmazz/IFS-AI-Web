using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using IFS.AIWeb.Application;
using Microsoft.AspNetCore.Mvc;

namespace IFS.AIWeb.Api;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var admin = endpoints.MapGroup("/api/admin").RequireAuthorization("AdminOnly");

        admin.MapGet("/users", async (int? page, int? pageSize, string? search, string? role,
            string? isActive, AdminService service, CancellationToken ct) =>
            Results.Ok(await service.GetUsersAsync(new(page ?? 1, pageSize ?? 20, search, role,
                ParseOptionalBool(isActive, "isActive")), ct)));

        admin.MapPost("/users", async (AdminCreateUserRequest request, AdminService service, CancellationToken ct) =>
            Results.Json(await service.CreateUserAsync(new(request.Username, request.FirstName, request.LastName,
                request.Password, request.PasswordConfirmation, request.Role), ct), statusCode: StatusCodes.Status201Created))
            .WithMetadata(new RequestSizeLimitAttribute(16_384));

        admin.MapPatch("/users/{id:guid}/status", async (Guid id, AdminUserStatusRequest request,
            ClaimsPrincipal principal, AdminService service, CancellationToken ct) =>
            Results.Ok(await service.SetStatusAsync(new(
                Guid.Parse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub)!), id,
                request.IsActive ?? throw Validation("isActive", "Aktiflik durumu zorunludur.")), ct)));

        admin.MapPut("/users/{id:guid}/password", async (Guid id, AdminPasswordRequest request,
            AdminService service, CancellationToken ct) =>
        {
            await service.ResetPasswordAsync(new(id, request.Password, request.PasswordConfirmation), ct);
            return Results.NoContent();
        }).WithMetadata(new RequestSizeLimitAttribute(16_384));

        admin.MapGet("/logs", async (int? page, int? pageSize, string? status, string? language,
            string? user, string? fromUtc, string? toUtc, AdminService service, CancellationToken ct) =>
            Results.Ok(await service.GetLogsAsync(new(page ?? 1, pageSize ?? 20, status, language, user,
                ParseOptionalDate(fromUtc, "fromUtc"), ParseOptionalDate(toUtc, "toUtc")), ct)));

        admin.MapGet("/prompt-info", (AdminService service) => Results.Ok(service.GetPromptInfo()));
        admin.MapGet("/statistics/seven-days", async (AdminService service, CancellationToken ct) =>
            Results.Ok(await service.GetSevenDayStatisticsAsync(ct)));
        return endpoints;
    }

    private static bool? ParseOptionalBool(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (bool.TryParse(value, out var parsed)) return parsed;
        throw Validation(field, "Değer true veya false olmalıdır.");
    }

    private static DateTimeOffset? ParseOptionalDate(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed)) return parsed;
        throw Validation(field, "Geçerli bir UTC tarih ve saat değeri girilmelidir.");
    }

    private static RequestValidationException Validation(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });
}

public sealed record AdminCreateUserRequest(string Username, string FirstName, string LastName,
    string Password, string PasswordConfirmation, string Role);
public sealed record AdminUserStatusRequest(bool? IsActive);
public sealed record AdminPasswordRequest(string Password, string PasswordConfirmation);

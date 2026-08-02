using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using IFS.AIWeb.Application;
using IFS.AIWeb.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

const string CorsPolicy = "SpaCors";
var builder = WebApplication.CreateBuilder(args);
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new();
if (!builder.Environment.IsEnvironment("Testing") && Encoding.UTF8.GetByteCount(jwt.SigningKey) < 32) throw new InvalidOperationException("JWT imzalama anahtarı en az 32 bayt olmalıdır.");
builder.Services.AddApplication(); builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddCors(o => o.AddPolicy(CorsPolicy, p => p.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.MapInboundClaims = false; o.TokenValidationParameters = new()
    { ValidateIssuer = true, ValidIssuer = jwt.Issuer, ValidateAudience = true, ValidAudience = jwt.Audience, ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)), ValidateLifetime = true, ClockSkew = TimeSpan.FromSeconds(30), NameClaimType = JwtRegisteredClaimNames.Sub, RoleClaimType = ClaimTypes.Role };
});
builder.Services.AddAuthorization(o =>
{
    o.DefaultPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().AddRequirements(new ActiveUserRequirement()).Build();
    o.AddPolicy("AdminOnly", p => p.RequireAuthenticatedUser().RequireRole("Admin").AddRequirements(new ActiveUserRequirement()));
});
builder.Services.AddScoped<IAuthorizationHandler, ActiveUserHandler>();

var app = builder.Build(); app.UseExceptionHandler(handler => handler.Run(WriteError)); app.UseCors(CorsPolicy); app.UseAuthentication(); app.UseAuthorization();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" })).AllowAnonymous();
var auth = app.MapGroup("/api/auth");
auth.MapPost("/register", async (RegisterRequest request, AuthService service, CancellationToken ct) => Results.Created("/api/auth/me", await service.RegisterAsync(new(request.Username, request.FirstName, request.LastName, request.Password, request.PasswordConfirmation), ct))).AllowAnonymous();
auth.MapPost("/login", async (LoginRequest request, AuthService service, HttpContext context, CancellationToken ct) =>
{ var result = await service.LoginAsync(new(request.Username, request.Password), ct); SetRefreshCookie(context, result.RefreshToken, result.RefreshTokenExpiresAtUtc); return Results.Ok(new { result.AccessToken, result.AccessTokenExpiresAtUtc, result.User }); }).AllowAnonymous();
auth.MapPost("/refresh", async (AuthService service, HttpContext context, CancellationToken ct) =>
{ ValidateOrigin(context, allowedOrigins); if (!context.Request.Cookies.TryGetValue("refreshToken", out var token)) throw new AuthenticationFailedException(); var result = await service.RefreshAsync(token, ct); SetRefreshCookie(context, result.RefreshToken, result.RefreshTokenExpiresAtUtc); return Results.Ok(new { result.AccessToken, result.AccessTokenExpiresAtUtc, result.User }); }).AllowAnonymous();
auth.MapPost("/logout", async (AuthService service, HttpContext context, CancellationToken ct) =>
{ if (context.Request.Cookies.TryGetValue("refreshToken", out var token)) { ValidateOrigin(context, allowedOrigins); await service.LogoutAsync(token, ct); } context.Response.Cookies.Delete("refreshToken", CookieOptions(context, DateTimeOffset.UnixEpoch)); return Results.NoContent(); }).AllowAnonymous();
auth.MapGet("/me", async (ClaimsPrincipal principal, AuthService service, CancellationToken ct) => Results.Ok(await service.GetUserAsync(Guid.Parse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub)!), ct))).RequireAuthorization();
auth.MapGet("/admin-check", () => Results.Ok(new { message = "Yönetici yetkilendirmesi doğrulandı." })).RequireAuthorization("AdminOnly");
if (!app.Environment.IsEnvironment("Testing")) await FirstAdminSeeder.SeedAsync(app.Services, app.Configuration);
app.Run();

static void SetRefreshCookie(HttpContext context, string token, DateTimeOffset expires) => context.Response.Cookies.Append("refreshToken", token, CookieOptions(context, expires));
static CookieOptions CookieOptions(HttpContext context, DateTimeOffset expires) => new() { HttpOnly = true, Secure = !context.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment(), SameSite = SameSiteMode.Strict, Path = "/api/auth", Expires = expires, MaxAge = expires > DateTimeOffset.UtcNow ? expires - DateTimeOffset.UtcNow : TimeSpan.Zero };
static void ValidateOrigin(HttpContext context, string[] allowed) { var origin = context.Request.Headers.Origin.ToString(); if (string.IsNullOrWhiteSpace(origin) || !allowed.Contains(origin, StringComparer.Ordinal)) throw new BadHttpRequestException("İstek kaynağına izin verilmiyor.", 403); }
static async Task WriteError(HttpContext context)
{
    var error = context.Features.Get<IExceptionHandlerFeature>()!.Error; var (status, title) = error switch { RequestValidationException => (400, "Doğrulama hatası"), AuthenticationFailedException => (401, "Kimlik doğrulama başarısız"), UsernameConflictException => (409, "Kullanıcı adı kullanılıyor"), BadHttpRequestException bad => (bad.StatusCode, "İstek reddedildi"), _ => (500, "Beklenmeyen hata") };
    context.Response.StatusCode = status; var problem = new ProblemDetails { Status = status, Title = title, Detail = status == 401 ? "Kullanıcı adı veya şifre geçersiz ya da oturum kullanılamıyor." : status == 500 ? "İstek işlenemedi." : error.Message };
    if (error is RequestValidationException validation) problem.Extensions["errors"] = validation.Errors; await context.Response.WriteAsJsonAsync(problem);
}
public sealed record RegisterRequest(string Username, string FirstName, string LastName, string Password, string PasswordConfirmation);
public sealed record LoginRequest(string Username, string Password);
public sealed class ActiveUserRequirement : IAuthorizationRequirement;
public sealed class ActiveUserHandler(IUserRepository users) : AuthorizationHandler<ActiveUserRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, ActiveUserRequirement requirement)
    { if (Guid.TryParse(context.User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id) && (await users.FindByIdAsync(id, CancellationToken.None))?.IsActive == true) context.Succeed(requirement); }
}
public partial class Program;

using IFS.AIWeb.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IFS.AIWeb.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connection = configuration.GetConnectionString("PostgreSql") ?? throw new InvalidOperationException("PostgreSQL bağlantısı yapılandırılmalıdır.");
        services.AddDbContext<AuthDbContext>(o => o.UseNpgsql(connection));
        var jwt = configuration.GetSection("Jwt").Get<JwtOptions>() ?? new(); services.AddSingleton(jwt);
        services.AddScoped<IUserRepository, UserRepository>(); services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>(); services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddSingleton<IPasswordService, PasswordService>(); services.AddSingleton<IRefreshTokenService, RefreshTokenService>(); services.AddSingleton<IClock, SystemClock>(); services.AddSingleton<IAccessTokenService, AccessTokenService>();
        return services;
    }
}

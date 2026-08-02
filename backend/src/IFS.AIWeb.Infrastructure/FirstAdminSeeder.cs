using IFS.AIWeb.Application;
using IFS.AIWeb.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IFS.AIWeb.Infrastructure;

public static class FirstAdminSeeder
{
    public static async Task SeedAsync(IServiceProvider provider, IConfiguration configuration, CancellationToken ct = default)
    {
        var username = configuration["InitialAdmin:Username"]; var password = configuration["InitialAdmin:Password"];
        if (string.IsNullOrWhiteSpace(username) && string.IsNullOrWhiteSpace(password)) return;
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password)) throw new InvalidOperationException("İlk yönetici için iki yapılandırma değeri de gereklidir.");
        var (display, normalized) = AuthValidation.Username(username); AuthValidation.Password(password);
        await using var scope = provider.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var existing = await db.Users.SingleOrDefaultAsync(x => x.NormalizedUsername == normalized, ct);
        if (existing is not null)
        { if (existing.Role == UserRole.Admin) return; throw new InvalidOperationException("İlk yönetici kullanıcı adı başka bir hesapla çakışıyor; mevcut hesap yükseltilmedi."); }
        var now = DateTimeOffset.UtcNow; var user = User.Create(Guid.NewGuid(), display, normalized, "İlk", "Yönetici", string.Empty, UserRole.Admin, now);
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordService>(); user = User.Create(user.Id, display, normalized, "İlk", "Yönetici", hasher.Hash(user, password), UserRole.Admin, now);
        db.Users.Add(user); await db.SaveChangesAsync(ct);
    }
}

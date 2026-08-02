using IFS.AIWeb.Domain;
using Microsoft.EntityFrameworkCore;

namespace IFS.AIWeb.Infrastructure;

public sealed class AuthDbContext(DbContextOptions<AuthDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var user = modelBuilder.Entity<User>(); user.ToTable("users"); user.HasKey(x => x.Id);
        user.Property(x => x.Id).HasColumnName("id"); user.Property(x => x.Username).HasColumnName("username").HasMaxLength(32).IsRequired();
        user.Property(x => x.NormalizedUsername).HasColumnName("normalized_username").HasMaxLength(64).IsRequired();
        user.HasIndex(x => x.NormalizedUsername).IsUnique().HasDatabaseName("ux_users_normalized_username");
        user.Property(x => x.FirstName).HasColumnName("first_name").HasMaxLength(80).IsRequired(); user.Property(x => x.LastName).HasColumnName("last_name").HasMaxLength(80).IsRequired();
        user.Property(x => x.PasswordHash).HasColumnName("password_hash").HasMaxLength(512).IsRequired(); user.Property(x => x.Role).HasColumnName("role").HasConversion<string>().HasMaxLength(16).IsRequired();
        user.Property(x => x.IsActive).HasColumnName("is_active"); user.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone"); user.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("timestamp with time zone");

        var token = modelBuilder.Entity<RefreshToken>(); token.ToTable("refresh_tokens"); token.HasKey(x => x.Id);
        token.Property(x => x.Id).HasColumnName("id"); token.Property(x => x.UserId).HasColumnName("user_id"); token.Property(x => x.TokenHash).HasColumnName("token_hash").HasMaxLength(64).IsRequired(); token.HasIndex(x => x.TokenHash).IsUnique().HasDatabaseName("ux_refresh_tokens_token_hash");
        token.Property(x => x.FamilyId).HasColumnName("family_id"); token.HasIndex(x => x.FamilyId).HasDatabaseName("ix_refresh_tokens_family_id");
        token.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone"); token.Property(x => x.ExpiresAtUtc).HasColumnName("expires_at_utc").HasColumnType("timestamp with time zone"); token.Property(x => x.RevokedAtUtc).HasColumnName("revoked_at_utc").HasColumnType("timestamp with time zone");
        token.Property(x => x.ReplacedByTokenId).HasColumnName("replaced_by_token_id"); token.Property(x => x.RevocationReason).HasColumnName("revocation_reason").HasMaxLength(100);
        token.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

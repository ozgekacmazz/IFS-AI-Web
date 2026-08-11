using IFS.AIWeb.Domain;
using Microsoft.EntityFrameworkCore;

namespace IFS.AIWeb.Infrastructure;

public sealed class AuthDbContext(DbContextOptions<AuthDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<SummaryRecord> SummaryRecords => Set<SummaryRecord>();
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
        token.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone"); token.Property(x => x.ExpiresAtUtc).HasColumnName("expires_at_utc").HasColumnType("timestamp with time zone"); token.Property(x => x.AbsoluteExpiresAtUtc).HasColumnName("absolute_expires_at_utc").HasColumnType("timestamp with time zone"); token.Property(x => x.RevokedAtUtc).HasColumnName("revoked_at_utc").HasColumnType("timestamp with time zone");
        token.Property(x => x.ReplacedByTokenId).HasColumnName("replaced_by_token_id"); token.Property(x => x.RevocationReason).HasColumnName("revocation_reason").HasMaxLength(100);
        token.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);

        var summary = modelBuilder.Entity<SummaryRecord>(); summary.ToTable("summary_records"); summary.HasKey(x => x.Id);
        summary.Property(x => x.Id).HasColumnName("id"); summary.Property(x => x.UserId).HasColumnName("user_id");
        summary.Property(x => x.InputText).HasColumnName("input_text").HasMaxLength(12000).IsRequired(); summary.Property(x => x.SummaryText).HasColumnName("summary_text").HasMaxLength(8000);
        summary.Property(x => x.RequestedLanguage).HasColumnName("requested_language").HasConversion<string>().HasMaxLength(16);
        summary.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(16);
        summary.Property(x => x.Provider).HasColumnName("provider").HasMaxLength(32); summary.Property(x => x.Model).HasColumnName("model").HasMaxLength(128);
        summary.Property(x => x.PromptVersion).HasColumnName("prompt_version").HasMaxLength(32); summary.Property(x => x.InputCharacterCount).HasColumnName("input_character_count"); summary.Property(x => x.OutputCharacterCount).HasColumnName("output_character_count");
        summary.Property(x => x.DurationMilliseconds).HasColumnName("duration_milliseconds"); summary.Property(x => x.FailureCode).HasColumnName("failure_code").HasMaxLength(32);
        summary.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone"); summary.Property(x => x.ExpiresAtUtc).HasColumnName("expires_at_utc").HasColumnType("timestamp with time zone");
        summary.Property(x => x.Feedback).HasColumnName("feedback").HasConversion<string>().HasMaxLength(16);
        summary.Property(x => x.FeedbackUpdatedAtUtc).HasColumnName("feedback_updated_at_utc").HasColumnType("timestamp with time zone");
        summary.ToTable(table =>
        {
            table.HasCheckConstraint("ck_summary_records_feedback_value", "feedback IS NULL OR feedback IN ('Useful', 'NotUseful')");
            table.HasCheckConstraint("ck_summary_records_feedback_timestamp", "(feedback IS NULL AND feedback_updated_at_utc IS NULL) OR (feedback IS NOT NULL AND feedback_updated_at_utc IS NOT NULL)");
        });
        summary.HasIndex(x => new { x.UserId, x.Status, x.CreatedAtUtc }).HasDatabaseName("ix_summary_records_user_status_created"); summary.HasIndex(x => x.ExpiresAtUtc).HasDatabaseName("ix_summary_records_expires_at"); summary.HasIndex(x => x.CreatedAtUtc).HasDatabaseName("ix_summary_records_created_at");
        summary.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

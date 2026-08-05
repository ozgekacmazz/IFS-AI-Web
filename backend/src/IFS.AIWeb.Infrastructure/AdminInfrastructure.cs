using IFS.AIWeb.Application;
using IFS.AIWeb.Domain;
using Microsoft.EntityFrameworkCore;

namespace IFS.AIWeb.Infrastructure;

internal sealed class AdminRepository(AuthDbContext db) : IAdminRepository
{
    private const long AdminMutationLock = 4_946_535_241;

    public async Task<AdminUserPage> GetUsersAsync(int page, int pageSize, string? normalizedSearch,
        UserRole? role, bool? isActive, CancellationToken ct)
    {
        var query = db.Users.AsNoTracking();
        if (normalizedSearch is not null)
        {
            var escaped = normalizedSearch.Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal);
            var pattern = $"%{escaped}%";
            query = query.Where(x => EF.Functions.ILike(x.NormalizedUsername, pattern, "\\") ||
                EF.Functions.ILike(x.FirstName, pattern, "\\") || EF.Functions.ILike(x.LastName, pattern, "\\"));
        }
        if (role is not null) query = query.Where(x => x.Role == role);
        if (isActive is not null) query = query.Where(x => x.IsActive == isActive);
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new(items, total);
    }

    public Task<User?> FindTrackedUserAsync(Guid id, CancellationToken ct) =>
        db.Users.SingleOrDefaultAsync(x => x.Id == id, ct);

    public async Task<AdminUserMutationResult> SetUserStatusAsync(Guid actorId, Guid targetId, bool isActive,
        DateTimeOffset now, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({AdminMutationLock})", ct);
        var target = await db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id = {targetId} FOR UPDATE")
            .SingleOrDefaultAsync(ct);
        if (target is null) return AdminUserMutationResult.NotFound;
        if (!isActive && targetId == actorId) return AdminUserMutationResult.SelfDeactivation;
        if (!isActive && target.IsActive && target.Role == UserRole.Admin &&
            await db.Users.CountAsync(x => x.IsActive && x.Role == UserRole.Admin, ct) <= 1)
            return AdminUserMutationResult.LastActiveAdmin;

        if (isActive) target.Activate(now); else target.Deactivate(now);
        if (!isActive) await RevokeUserSessionsAsync(targetId, now, "Kullanıcı yönetici tarafından pasife alındı", ct);
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return AdminUserMutationResult.Updated;
    }

    public async Task<bool> ResetPasswordAsync(Guid targetId, string passwordHash, DateTimeOffset now,
        CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var target = await db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id = {targetId} FOR UPDATE")
            .SingleOrDefaultAsync(ct);
        if (target is null) return false;
        target.ChangePasswordHash(passwordHash, now);
        await RevokeUserSessionsAsync(targetId, now, "Şifre yönetici tarafından güncellendi", ct);
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return true;
    }

    public async Task<AdminLogPage> GetLogsAsync(int page, int pageSize, SummaryStatus? status,
        SummaryLanguage? language, string? normalizedUser, DateTimeOffset? fromUtc, DateTimeOffset? toUtc,
        int prefixLength, CancellationToken ct)
    {
        var query = db.SummaryRecords.AsNoTracking();
        if (status is not null) query = query.Where(x => x.Status == status);
        if (language is not null) query = query.Where(x => x.RequestedLanguage == language);
        if (normalizedUser is not null)
        {
            var escaped = normalizedUser.Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal);
            var pattern = $"%{escaped}%";
            query = query.Where(x => EF.Functions.ILike(x.User.NormalizedUsername, pattern, "\\"));
        }
        if (fromUtc is not null) query = query.Where(x => x.CreatedAtUtc >= fromUtc);
        if (toUtc is not null) query = query.Where(x => x.CreatedAtUtc < toUtc);
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new AdminLogProjection(x.Id, x.CreatedAtUtc, x.ExpiresAtUtc, x.User.Username,
                x.Status, x.RequestedLanguage, x.Provider, x.Model, x.PromptVersion, x.DurationMilliseconds,
                x.InputCharacterCount, x.OutputCharacterCount, x.FailureCode,
                x.InputText.Length <= prefixLength ? x.InputText : x.InputText.Substring(0, prefixLength),
                x.SummaryText == null ? null : x.SummaryText.Length <= prefixLength ? x.SummaryText : x.SummaryText.Substring(0, prefixLength)))
            .ToListAsync(ct);
        return new(items, total);
    }

    public async Task<AdminStatisticsData> GetStatisticsAsync(DateTimeOffset fromUtc,
        DateTimeOffset toExclusiveUtc, CancellationToken ct)
    {
        var days = await db.Database.SqlQuery<StatisticsDayRow>($$"""
            SELECT created_at_utc::date AS "DateUtc",
                   COUNT(*)::int AS "Total",
                   COUNT(*) FILTER (WHERE status = 'Succeeded')::int AS "Succeeded",
                   COUNT(*) FILTER (WHERE status = 'Failed')::int AS "Failed",
                   COUNT(*) FILTER (WHERE requested_language = 'Turkish')::int AS "Turkish",
                   COUNT(*) FILTER (WHERE requested_language = 'English')::int AS "English",
                   COALESCE(AVG(duration_milliseconds), 0)::numeric AS "AverageDurationMilliseconds",
                   COUNT(DISTINCT user_id)::int AS "ActiveUsers"
            FROM summary_records
            WHERE created_at_utc >= {{fromUtc}} AND created_at_utc < {{toExclusiveUtc}}
            GROUP BY created_at_utc::date
            """).ToListAsync(ct);
        var providers = await db.Database.SqlQuery<ProviderRow>($$"""
            SELECT provider AS "Provider", model AS "Model", COUNT(*)::int AS "Total"
            FROM summary_records
            WHERE created_at_utc >= {{fromUtc}} AND created_at_utc < {{toExclusiveUtc}}
            GROUP BY provider, model
            ORDER BY COUNT(*) DESC, provider, model
            """).ToListAsync(ct);
        var activeUsers = await db.Database.SqlQuery<int>($$"""
            SELECT COUNT(DISTINCT user_id)::int AS "Value"
            FROM summary_records
            WHERE created_at_utc >= {{fromUtc}} AND created_at_utc < {{toExclusiveUtc}}
            """).SingleAsync(ct);
        var feedbackRow = await db.Database.SqlQuery<FeedbackRow>($$"""
            SELECT COUNT(*) FILTER (WHERE feedback = 'Useful')::int AS "Useful",
                   COUNT(*) FILTER (WHERE feedback = 'NotUseful')::int AS "NotUseful"
            FROM summary_records
            WHERE created_at_utc >= {{fromUtc}} AND created_at_utc < {{toExclusiveUtc}}
            """).SingleAsync(ct);
        var totalFeedback = feedbackRow.Useful + feedbackRow.NotUseful;
        var satisfactionRate = totalFeedback == 0 ? 0 : Math.Round((decimal)feedbackRow.Useful * 100 / totalFeedback, 2);
        var feedbackSummary = new AdminFeedbackSummary(feedbackRow.Useful, feedbackRow.NotUseful, satisfactionRate);
        return new(days.Select(x => new AdminStatisticsDayProjection(x.DateUtc, x.Total, x.Succeeded,
                x.Failed, x.Turkish, x.English, x.AverageDurationMilliseconds, x.ActiveUsers)).ToArray(),
            providers.Select(x => new AdminProviderStatistics(x.Provider, x.Model, x.Total)).ToArray(), activeUsers, feedbackSummary);
    }

    private async Task RevokeUserSessionsAsync(Guid userId, DateTimeOffset now, string reason, CancellationToken ct)
    {
        var tokens = await db.RefreshTokens.Where(x => x.UserId == userId && x.RevokedAtUtc == null).ToListAsync(ct);
        foreach (var token in tokens) token.Revoke(now, reason);
    }

    private sealed record StatisticsDayRow(DateOnly DateUtc, int Total, int Succeeded, int Failed,
        int Turkish, int English, decimal AverageDurationMilliseconds, int ActiveUsers);
    private sealed record ProviderRow(string Provider, string Model, int Total);
    private sealed record FeedbackRow(int Useful, int NotUseful);
}

using IFS.AIWeb.Domain;

namespace IFS.AIWeb.Domain.Tests;

public sealed class AuthModelsTests
{
    [Fact] public void NewUser_IsActiveWithRequestedRole()
    { var now = DateTimeOffset.UtcNow; var user = User.Create(Guid.NewGuid(), "member", "MEMBER", "Test", "User", "hash", UserRole.User, now); Assert.True(user.IsActive); Assert.Equal(UserRole.User, user.Role); Assert.Equal(now, user.CreatedAtUtc); }
    [Fact] public void RefreshToken_RevokeIsIdempotentAndTracksReplacement()
    { var now = DateTimeOffset.UtcNow; var replacement = Guid.NewGuid(); var token = RefreshToken.Create(Guid.NewGuid(), Guid.NewGuid(), "hash", Guid.NewGuid(), now, now.AddDays(7)); token.Revoke(now.AddMinutes(1), "rotation", replacement); token.Revoke(now.AddMinutes(2), "other"); Assert.Equal(replacement, token.ReplacedByTokenId); Assert.Equal("rotation", token.RevocationReason); }
    [Fact] public void AdminMutationsUpdateStateAndTimestampExplicitly()
    {
        var created = DateTimeOffset.UtcNow; var user = User.Create(Guid.NewGuid(), "member", "MEMBER", "Test", "User", "old-hash", UserRole.User, created);
        user.Deactivate(created.AddMinutes(1)); Assert.False(user.IsActive); Assert.Equal(created.AddMinutes(1), user.UpdatedAtUtc);
        user.Activate(created.AddMinutes(2)); Assert.True(user.IsActive); Assert.Equal(created.AddMinutes(2), user.UpdatedAtUtc);
        user.ChangePasswordHash("new-hash", created.AddMinutes(3)); Assert.Equal("new-hash", user.PasswordHash); Assert.Equal(created.AddMinutes(3), user.UpdatedAtUtc);
    }
}

using IFS.AIWeb.Domain;

namespace IFS.AIWeb.Domain.Tests;

public sealed class AuthModelsTests
{
    [Fact] public void NewUser_IsActiveWithRequestedRole()
    { var now = DateTimeOffset.UtcNow; var user = User.Create(Guid.NewGuid(), "member", "MEMBER", "Test", "User", "hash", UserRole.User, now); Assert.True(user.IsActive); Assert.Equal(UserRole.User, user.Role); Assert.Equal(now, user.CreatedAtUtc); }
    [Fact] public void RefreshToken_RevokeIsIdempotentAndTracksReplacement()
    { var now = DateTimeOffset.UtcNow; var replacement = Guid.NewGuid(); var token = RefreshToken.Create(Guid.NewGuid(), Guid.NewGuid(), "hash", Guid.NewGuid(), now, now.AddDays(7)); token.Revoke(now.AddMinutes(1), "rotation", replacement); token.Revoke(now.AddMinutes(2), "other"); Assert.Equal(replacement, token.ReplacedByTokenId); Assert.Equal("rotation", token.RevocationReason); }
}

using FluentAssertions;
using Stockma.Domain.Entities;
using Stockma.Domain.Exceptions;

namespace Stockma.Domain.Tests;

public class RefreshTokenTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTimeOffset IssuedAt = new(2026, 9, 28, 10, 0, 0, TimeSpan.Zero);

    private const string DeviceId = "device-abc-0123456789";
    private const string TokenHash = "hash-1";
    private const int SessionIdleTimeoutMinutes = 30;
    private const int RefreshTokenLifetimeHours = 8;

    private static RefreshToken IssueNewFamily(
        DateTimeOffset? issuedAt = null,
        int sessionIdleTimeoutMinutes = SessionIdleTimeoutMinutes,
        int refreshTokenLifetimeHours = RefreshTokenLifetimeHours,
        string tokenHash = TokenHash) =>
        RefreshToken.IssueForNewFamily(
            TenantId,
            UserId,
            DeviceId,
            tokenHash,
            issuedAt ?? IssuedAt,
            sessionIdleTimeoutMinutes,
            refreshTokenLifetimeHours);

    [Fact]
    public void IssueForNewFamily_RejectsEmptyTenantId()
    {
        var act = () => RefreshToken.IssueForNewFamily(
            Guid.Empty, UserId, DeviceId, TokenHash, IssuedAt, SessionIdleTimeoutMinutes, RefreshTokenLifetimeHours);

        act.Should().Throw<ArgumentException>().WithParameterName("tenantId");
    }

    [Fact]
    public void IssueForNewFamily_RejectsEmptyUserId()
    {
        var act = () => RefreshToken.IssueForNewFamily(
            TenantId, Guid.Empty, DeviceId, TokenHash, IssuedAt, SessionIdleTimeoutMinutes, RefreshTokenLifetimeHours);

        act.Should().Throw<ArgumentException>().WithParameterName("userId");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void IssueForNewFamily_RejectsBlankDeviceId(string deviceId)
    {
        var act = () => RefreshToken.IssueForNewFamily(
            TenantId, UserId, deviceId, TokenHash, IssuedAt, SessionIdleTimeoutMinutes, RefreshTokenLifetimeHours);

        act.Should().Throw<ArgumentException>().WithParameterName("deviceId");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void IssueForNewFamily_RejectsBlankTokenHash(string tokenHash)
    {
        var act = () => RefreshToken.IssueForNewFamily(
            TenantId, UserId, DeviceId, tokenHash, IssuedAt, SessionIdleTimeoutMinutes, RefreshTokenLifetimeHours);

        act.Should().Throw<ArgumentException>().WithParameterName("tokenHash");
    }

    [Fact]
    public void IssueForNewFamily_RejectsNonPositiveIdleTimeout()
    {
        var act = () => RefreshToken.IssueForNewFamily(
            TenantId, UserId, DeviceId, TokenHash, IssuedAt, 0, RefreshTokenLifetimeHours);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("sessionIdleTimeoutMinutes");
    }

    [Fact]
    public void IssueForNewFamily_RejectsNonPositiveLifetimeHours()
    {
        var act = () => RefreshToken.IssueForNewFamily(
            TenantId, UserId, DeviceId, TokenHash, IssuedAt, SessionIdleTimeoutMinutes, 0);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("refreshTokenLifetimeHours");
    }

    [Fact]
    public void IssueForNewFamily_SetsFamilyExpiresAtToLoginPlusLifetimeHours()
    {
        var token = IssueNewFamily();

        token.FamilyExpiresAt.Should().Be(IssuedAt.AddHours(RefreshTokenLifetimeHours), "data-model.md 3c");
    }

    [Fact]
    public void IssueForNewFamily_GivesEachTokenItsOwnFamilyId()
    {
        var first = IssueNewFamily();
        var second = IssueNewFamily();

        first.FamilyId.Should().NotBe(second.FamilyId, "cada login abre su propia familia");
    }

    [Fact]
    public void ExpiresAt_FarFromTheCap_IsIssuedAtPlusIdleTimeout()
    {
        var token = IssueNewFamily(sessionIdleTimeoutMinutes: 30, refreshTokenLifetimeHours: 8);

        token.ExpiresAt.Should().Be(
            IssuedAt.AddMinutes(30),
            "ADR-019: lejos del tope absoluto, el vencimiento lo pone la inactividad");
    }

    [Fact]
    public void ExpiresAt_NearTheCap_IsCappedAtFamilyExpiresAt()
    {
        var first = IssueNewFamily(sessionIdleTimeoutMinutes: 30, refreshTokenLifetimeHours: RefreshTokenLifetimeHours);
        var rotatedAt = first.FamilyExpiresAt.AddMinutes(-10);

        var rotated = first.CreateNextInFamily("hash-2", rotatedAt, sessionIdleTimeoutMinutes: 30);

        rotated.ExpiresAt.Should().Be(
            rotated.FamilyExpiresAt,
            "ADR-019: cerca del tope, la inactividad no puede extender la sesión más allá de la familia");
    }

    [Fact]
    public void CreateNextInFamily_InheritsFamilyIdAndDeviceId()
    {
        var first = IssueNewFamily();

        var next = first.CreateNextInFamily("hash-2", IssuedAt.AddMinutes(5), SessionIdleTimeoutMinutes);

        next.FamilyId.Should().Be(first.FamilyId);
        next.DeviceId.Should().Be(first.DeviceId);
        next.TenantId.Should().Be(first.TenantId);
        next.UserId.Should().Be(first.UserId);
    }

    [Fact]
    public void CreateNextInFamily_InheritsFamilyExpiresAtUnchanged()
    {
        var first = IssueNewFamily();

        var next = first.CreateNextInFamily("hash-2", IssuedAt.AddMinutes(5), SessionIdleTimeoutMinutes);

        next.FamilyExpiresAt.Should().Be(
            first.FamilyExpiresAt,
            "data-model.md 3c: los tokens rotados heredan el tope absoluto sin cambios");
    }

    [Fact]
    public void CreateNextInFamily_GetsItsOwnId()
    {
        var first = IssueNewFamily();

        var next = first.CreateNextInFamily("hash-2", IssuedAt.AddMinutes(5), SessionIdleTimeoutMinutes);

        next.Id.Should().NotBe(first.Id);
    }

    [Fact]
    public void CreateNextInFamily_IsBornUnconsumedAndUnrevoked()
    {
        var first = IssueNewFamily();

        var next = first.CreateNextInFamily("hash-2", IssuedAt.AddMinutes(5), SessionIdleTimeoutMinutes);

        next.ConsumedAt.Should().BeNull();
        next.RevokedAt.Should().BeNull();
    }

    [Fact]
    public void NewToken_IsUsable()
    {
        IssueNewFamily().IsUsable(IssuedAt).Should().BeTrue();
    }

    [Fact]
    public void Token_IsUsable_UntilTheInstantItExpires()
    {
        var token = IssueNewFamily();

        token.IsUsable(token.ExpiresAt.AddTicks(-1)).Should().BeTrue();
    }

    [Fact]
    public void Token_IsNotUsable_AtOrAfterExpiry()
    {
        var token = IssueNewFamily();

        token.IsUsable(token.ExpiresAt).Should().BeFalse();
    }

    [Fact]
    public void Consume_MarksTheTokenConsumed()
    {
        var token = IssueNewFamily();
        var consumedAt = IssuedAt.AddMinutes(1);

        token.Consume(consumedAt);

        token.ConsumedAt.Should().Be(consumedAt);
    }

    [Fact]
    public void Consume_AConsumedToken_Throws()
    {
        var token = IssueNewFamily();
        token.Consume(IssuedAt.AddMinutes(1));

        var act = () => token.Consume(IssuedAt.AddMinutes(2));

        act.Should().Throw<RefreshTokenNotUsableException>("ADR-018: presentar un token consumido es reuso");
    }

    [Fact]
    public void Consume_ARevokedToken_Throws()
    {
        var token = IssueNewFamily();
        token.Revoke(IssuedAt.AddMinutes(1));

        var act = () => token.Consume(IssuedAt.AddMinutes(2));

        act.Should().Throw<RefreshTokenNotUsableException>();
    }

    [Fact]
    public void Consume_AnExpiredToken_Throws()
    {
        var token = IssueNewFamily();

        var act = () => token.Consume(token.ExpiresAt);

        act.Should().Throw<RefreshTokenNotUsableException>();
    }

    [Fact]
    public void Revoke_MarksTheToken()
    {
        var token = IssueNewFamily();
        var revokedAt = IssuedAt.AddMinutes(1);

        token.Revoke(revokedAt);

        token.RevokedAt.Should().Be(revokedAt);
        token.IsUsable(revokedAt).Should().BeFalse();
    }

    [Fact]
    public void Revoke_IsIdempotent()
    {
        var token = IssueNewFamily();
        var firstRevocation = IssuedAt.AddMinutes(1);

        token.Revoke(firstRevocation);
        token.Revoke(IssuedAt.AddMinutes(2));

        token.RevokedAt.Should().Be(firstRevocation, "revocar dos veces no falla y conserva el primer instante");
    }

    [Fact]
    public void Revoke_RejectsAnInstantBeforeIssuedAt()
    {
        var token = IssueNewFamily();

        var act = () => token.Revoke(IssuedAt.AddSeconds(-1));

        act.Should().Throw<ArgumentException>().WithParameterName("revokedAt");
    }
}

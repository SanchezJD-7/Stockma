using FluentAssertions;
using Stockma.Domain.Entities;
using Stockma.Domain.ValueObjects;

namespace Stockma.Domain.Tests;

public class TenantSettingsTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static TenantSettings Create(
        int maxTrustedDevices = 2,
        int refreshTokenLifetimeHours = TenantSettings.DefaultRefreshTokenLifetimeHours,
        int sessionIdleTimeoutMinutes = TenantSettings.DefaultSessionIdleTimeoutMinutes,
        int trustedDeviceLifetimeDays = TenantSettings.DefaultTrustedDeviceLifetimeDays) =>
        new(TenantId, maxTrustedDevices, ExpiryThresholds.Default(), refreshTokenLifetimeHours, sessionIdleTimeoutMinutes, trustedDeviceLifetimeDays);

    [Fact]
    public void CreateDefault_UsesTheDocumentedDefaults()
    {
        var settings = TenantSettings.CreateDefault(TenantId);

        settings.RefreshTokenLifetimeHours.Should().Be(8, "data-model.md 1b");
        settings.SessionIdleTimeoutMinutes.Should().Be(120, "data-model.md 1b, ADR-019 enmendado por T092");
    }

    [Fact]
    public void RefreshTokenLifetimeHours_RejectsZero()
    {
        var act = () => Create(refreshTokenLifetimeHours: 0);

        act.Should()
            .Throw<ArgumentOutOfRangeException>("data-model.md: RefreshTokenLifetimeHours DEBE ser >= 1")
            .WithParameterName("refreshTokenLifetimeHours");
    }

    [Fact]
    public void RefreshTokenLifetimeHours_AcceptsOne()
    {
        var act = () => Create(refreshTokenLifetimeHours: 1, sessionIdleTimeoutMinutes: 16);

        act.Should().NotThrow();
    }

    [Fact]
    public void SessionIdleTimeoutMinutes_RejectsExactlyTheAccessTokenLifetime()
    {
        var act = () => Create(sessionIdleTimeoutMinutes: 15);

        act.Should()
            .Throw<ArgumentOutOfRangeException>(
                "data-model.md: DEBE ser mayor que la vigencia del access token (15 min, ADR-019)")
            .WithParameterName("sessionIdleTimeoutMinutes");
    }

    [Fact]
    public void SessionIdleTimeoutMinutes_RejectsLessThanTheAccessTokenLifetime()
    {
        var act = () => Create(sessionIdleTimeoutMinutes: 10);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("sessionIdleTimeoutMinutes");
    }

    [Fact]
    public void SessionIdleTimeoutMinutes_AcceptsOneMinuteAboveTheAccessTokenLifetime()
    {
        var act = () => Create(sessionIdleTimeoutMinutes: 16, refreshTokenLifetimeHours: 1);

        act.Should().NotThrow();
    }

    [Fact]
    public void SessionIdleTimeoutMinutes_RejectsExceedingTheFamilyLifetimeInMinutes()
    {
        var act = () => Create(refreshTokenLifetimeHours: 1, sessionIdleTimeoutMinutes: 61);

        act.Should()
            .Throw<ArgumentOutOfRangeException>(
                "data-model.md: SessionIdleTimeoutMinutes NO DEBE superar RefreshTokenLifetimeHours * 60")
            .WithParameterName("sessionIdleTimeoutMinutes");
    }

    [Fact]
    public void SessionIdleTimeoutMinutes_AcceptsExactlyTheFamilyLifetimeInMinutes()
    {
        var act = () => Create(refreshTokenLifetimeHours: 1, sessionIdleTimeoutMinutes: 60);

        act.Should().NotThrow();
    }

    [Fact]
    public void TrustedDeviceLifetimeDays_RejectsZero()
    {
        var act = () => Create(trustedDeviceLifetimeDays: 0);

        act.Should()
            .Throw<ArgumentOutOfRangeException>("data-model.md: TrustedDeviceLifetimeDays DEBE ser >= 1")
            .WithParameterName("trustedDeviceLifetimeDays");
    }

    [Fact]
    public void TrustedDeviceLifetimeDays_AcceptsOne()
    {
        var act = () => Create(trustedDeviceLifetimeDays: 1);

        act.Should().NotThrow();
    }

    [Fact]
    public void CreateDefault_UsesTheDocumentedTrustedDeviceLifetimeDays()
    {
        var settings = TenantSettings.CreateDefault(TenantId);

        settings.TrustedDeviceLifetimeDays.Should().Be(15, "data-model.md 1b, T070");
    }

    [Fact]
    public void CreateDefault_RequiresTheSecondFactor()
    {
        var settings = TenantSettings.CreateDefault(TenantId);

        settings.RequireSecondFactor
            .Should()
            .BeTrue("T090: fail-closed, nadie configuró nada y el 2FA sigue exigido");
    }

    [Fact]
    public void SetRequireSecondFactor_TurnsItOffAndBackOn()
    {
        var settings = TenantSettings.CreateDefault(TenantId);

        settings.SetRequireSecondFactor(false);
        settings.RequireSecondFactor.Should().BeFalse();

        settings.SetRequireSecondFactor(true);
        settings.RequireSecondFactor.Should().BeTrue();
    }
}

using FluentAssertions;
using Stockma.Domain.Entities;
using Stockma.Domain.ValueObjects;

namespace Stockma.Domain.Tests;

public class TenantBrandingTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void NewBranding_KeepsTheThreeConfigurableColors()
    {
        var branding = new TenantBranding("#8e24aa", "#6a1b9a", "#f3e5f5");

        branding.Primary.Should().Be("#8e24aa");
        branding.PrimaryActive.Should().Be("#6a1b9a");
        branding.PrimaryBg.Should().Be("#f3e5f5");
    }

    [Fact]
    public void NewBranding_NormalizesToLowercase()
    {
        var branding = new TenantBranding("#8E24AA", "#6A1B9A", "#F3E5F5");

        branding.Primary.Should().Be("#8e24aa");
    }

    [Fact]
    public void NewBranding_AcceptsShorthandHex()
    {
        new TenantBranding("#fff", "#000", "#abc").Primary.Should().Be("#fff");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("red")]
    [InlineData("8e24aa")]
    [InlineData("#12345")]
    [InlineData("#gggggg")]
    [InlineData("#8e24aa; background: url(evil)")]
    public void NewBranding_RejectsAnythingThatIsNotHex(string invalid)
    {
        var act = () => new TenantBranding(invalid, "#6a1b9a", "#f3e5f5");

        act.Should().Throw<ArgumentException>().WithParameterName("primary");
    }

    [Fact]
    public void NewBranding_RejectsNull()
    {
        var act = () => new TenantBranding("#8e24aa", null!, "#f3e5f5");

        act.Should().Throw<ArgumentException>().WithParameterName("primaryActive");
    }
}

public class TenantSettingsBrandingTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void NewSettings_HasNoBranding()
    {
        // Sin branding configurado el backend no guarda nada: los colores por
        // defecto viven en tokens.css y no se duplican acá.
        TenantSettings.CreateDefault(TenantId).Branding.Should().BeNull();
    }

    [Fact]
    public void UpdateBranding_StoresTheTenantColors()
    {
        var settings = TenantSettings.CreateDefault(TenantId);

        settings.UpdateBranding(new TenantBranding("#8e24aa", "#6a1b9a", "#f3e5f5"));

        settings.Branding!.Primary.Should().Be("#8e24aa");
    }

    [Fact]
    public void UpdateBranding_WithNull_FallsBackToTheDefaultPalette()
    {
        var settings = TenantSettings.CreateDefault(TenantId);
        settings.UpdateBranding(new TenantBranding("#8e24aa", "#6a1b9a", "#f3e5f5"));

        settings.UpdateBranding(null);

        settings.Branding.Should().BeNull();
    }
}

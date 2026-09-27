using FluentAssertions;
using Stockma.Domain.Exceptions;
using Stockma.Domain.ValueObjects;

namespace Stockma.Domain.Tests;

public class DeviceIdentifierTests
{
    [Fact]
    public void Normalize_TrimsSurroundingWhitespace()
    {
        DeviceIdentifier.Normalize("  web-chrome-a91f2c77  ").Should().Be("web-chrome-a91f2c77");
    }

    [Fact]
    public void Parse_ReturnsTheNormalizedValue()
    {
        DeviceIdentifier.Parse("  web-chrome-a91f2c77  ").Should().Be("web-chrome-a91f2c77");
    }

    [Fact]
    public void Parse_AcceptsAGuid()
    {
        var deviceId = Guid.NewGuid().ToString();

        DeviceIdentifier.Parse(deviceId).Should().Be(deviceId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("dev-1")]
    [InlineData("123456789012345")]
    [InlineData("   123456789012345   ")]
    public void Parse_RejectsMissingOrLowEntropyIdentifiers(string? deviceId)
    {
        var act = () => DeviceIdentifier.Parse(deviceId);

        act.Should()
            .Throw<ValidationFailedException>("ADR-016: la seguridad del dispositivo descansa en la entropía del deviceId")
            .Which.ErrorCode.Should().Be("VALIDATION_FAILED");
    }

    [Fact]
    public void Parse_AcceptsTheMinimumLength()
    {
        DeviceIdentifier.Parse(new string('a', DeviceIdentifier.MinLength)).Should().HaveLength(DeviceIdentifier.MinLength);
    }

    [Fact]
    public void Parse_RejectsIdentifiersLongerThanTheColumn()
    {
        var act = () => DeviceIdentifier.Parse(new string('a', DeviceIdentifier.MaxLength + 1));

        act.Should().Throw<ValidationFailedException>();
    }
}

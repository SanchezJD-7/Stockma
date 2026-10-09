using FluentAssertions;
using Stockma.Domain.Exceptions;
using Stockma.Domain.ValueObjects;

namespace Stockma.Domain.Tests;

public class PhoneNumberTests
{
    [Theory]
    [InlineData("+573001234567")]
    [InlineData("+5491123456789")]
    [InlineData("+1234567")]
    public void Parse_AcceptsE164Numbers(string value)
    {
        PhoneNumber.Parse(value).Should().Be(value);
    }

    [Fact]
    public void Parse_TrimsSurroundingWhitespace()
    {
        PhoneNumber.Parse("  +573001234567  ").Should().Be("+573001234567");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("3001234567")]
    [InlineData("+03001234567")]
    [InlineData("+57 300 123 456")]
    [InlineData("+123456")]
    [InlineData("+1234567890123456")]
    [InlineData("abc")]
    public void Parse_RejectsAnythingThatIsNotE164(string? value)
    {
        var act = () => PhoneNumber.Parse(value);

        act.Should()
            .Throw<ValidationFailedException>()
            .Which.ErrorCode.Should()
            .Be("VALIDATION_FAILED", "auth-api.md: 400 VALIDATION_FAILED, no un 500");
    }

    [Fact]
    public void Mask_FollowsTheContractExample()
    {
        PhoneNumber.Mask("+573001234567").Should().Be("+57300*****67", "auth-api.md fija phoneNumberMasked");
    }

    [Theory]
    [InlineData("+1234567", "********")]
    [InlineData("+5491122334455", "+54911******55")]
    public void Mask_HidesTheMiddleKeepingSixAndTwo(string value, string expected)
    {
        PhoneNumber.Mask(value).Should().Be(expected);
    }
}

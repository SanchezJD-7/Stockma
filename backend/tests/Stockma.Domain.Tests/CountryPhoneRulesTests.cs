using FluentAssertions;
using Stockma.Domain.Exceptions;
using Stockma.Domain.ValueObjects;

namespace Stockma.Domain.Tests;

public class CountryPhoneRulesTests
{
    [Fact]
    public void Validate_WithAColombianMobile_Passes()
    {
        var act = () => CountryPhoneRules.Validate("CO", "+573001234567");

        act.Should().NotThrow("un móvil colombiano de 10 dígitos que empieza con 3 es la forma esperada");
    }

    [Theory]
    [InlineData("+525512345678", "número mexicano: otro prefijo")]
    [InlineData("+5712345678", "fijo colombiano: no empieza con 3")]
    [InlineData("+57300123456", "9 dígitos: no llega a 10")]
    [InlineData("+5730012345678", "11 dígitos: se pasa de 10")]
    public void Validate_WithANonColombianOrMalformedNumber_Throws(string number, string because)
    {
        var act = () => CountryPhoneRules.Validate("CO", number);

        act.Should().Throw<ValidationFailedException>(because).WithMessage("*Colombia*");
    }

    [Fact]
    public void Validate_WithoutACountry_AcceptsAnythingE164()
    {
        var act = () => CountryPhoneRules.Validate(null, "+541123456789");

        act.Should().NotThrow("un tenant sin país configurado sólo exige E.164, ya validado por PhoneNumber.Parse");
    }

    [Fact]
    public void Validate_WithAnUnknownCountry_AcceptsAnythingE164()
    {
        var act = () => CountryPhoneRules.Validate("BR", "+5511987654321");

        act.Should().NotThrow("un país sin regla cargada no bloquea: la regla se agrega cuando se necesite");
    }

    [Fact]
    public void Find_ReturnsTheRuleForTheCountry()
    {
        CountryPhoneRules.Find("CO").Should().NotBeNull();
        CountryPhoneRules.Find("co").Should().NotBeNull("el código de país no distingue mayúsculas");
        CountryPhoneRules.Find("XX").Should().BeNull();
    }
}

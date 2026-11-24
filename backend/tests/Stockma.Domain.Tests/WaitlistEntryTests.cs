using FluentAssertions;
using Stockma.Domain.Entities;

namespace Stockma.Domain.Tests;

public class WaitlistEntryTests
{
    private static WaitlistEntry Create(
        string fullName = "Juan Pérez",
        string countryCode = "+57",
        string phone = "3001234567",
        string email = "juan@ejemplo.com") =>
        new(fullName, countryCode, phone, email);

    [Fact]
    public void NewWaitlistEntry_AssignsVersion7Id()
    {
        var entry = Create();

        entry.Id.Should().NotBeEmpty();
        entry.Id.Version.Should().Be(7, "las entidades del dominio usan Guid.CreateVersion7 (patrón de Product)");
    }

    [Fact]
    public void NewWaitlistEntry_NormalizesEmailToLowercaseAndTrims()
    {
        var entry = Create(email: "  Juan.Perez@Ejemplo.COM  ");

        entry.Email.Should().Be("juan.perez@ejemplo.com", "la unicidad se evalúa sobre el correo normalizado (spec 003)");
    }

    [Fact]
    public void NewWaitlistEntry_TrimsFullName()
    {
        var entry = Create(fullName: "  Juan Pérez  ");

        entry.FullName.Should().Be("Juan Pérez");
    }

    [Fact]
    public void NewWaitlistEntry_NormalizesPhoneToDigitsOnly()
    {
        var entry = Create(phone: "300 123 4567");

        entry.Phone.Should().Be("3001234567", "el teléfono se guarda sin espacios ni símbolos");
        entry.CountryCode.Should().Be("+57");
    }

    [Fact]
    public void NewWaitlistEntry_RecordsCreationTimestamp()
    {
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);

        var entry = Create();

        entry.CreatedAt.Should().BeOnOrAfter(before);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void NewWaitlistEntry_RejectsBlankFullName(string fullName)
    {
        var act = () => Create(fullName: fullName);

        act.Should().Throw<ArgumentException>().WithParameterName("fullName");
    }

    [Fact]
    public void NewWaitlistEntry_RejectsFullNameShorterThanTwoCharacters()
    {
        var act = () => Create(fullName: "A");

        act.Should().Throw<ArgumentException>().WithParameterName("fullName");
    }

    [Fact]
    public void NewWaitlistEntry_RejectsFullNameLongerThan120Characters()
    {
        var act = () => Create(fullName: new string('A', 121));

        act.Should().Throw<ArgumentException>().WithParameterName("fullName");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void NewWaitlistEntry_RejectsBlankEmail(string email)
    {
        var act = () => Create(email: email);

        act.Should().Throw<ArgumentException>().WithParameterName("email");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void NewWaitlistEntry_RejectsBlankPhone(string phone)
    {
        var act = () => Create(phone: phone);

        act.Should().Throw<ArgumentException>().WithParameterName("phone");
    }

    [Theory]
    [InlineData("123456")]
    [InlineData("3001234567890123")]
    [InlineData("300-abc-def")]
    public void NewWaitlistEntry_RejectsPhoneOutsideSevenToFifteenDigits(string phone)
    {
        var act = () => Create(phone: phone);

        act.Should().Throw<ArgumentException>().WithParameterName("phone");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void NewWaitlistEntry_RejectsBlankCountryCode(string countryCode)
    {
        var act = () => Create(countryCode: countryCode);

        act.Should().Throw<ArgumentException>().WithParameterName("countryCode");
    }

    [Fact]
    public void NewWaitlistEntry_RejectsEmailLongerThan256Characters()
    {
        var act = () => Create(email: new string('a', 257));

        act.Should().Throw<ArgumentException>().WithParameterName("email");
    }

    [Fact]
    public void NewWaitlistEntry_RejectsCountryCodeLongerThanEightCharacters()
    {
        var act = () => Create(countryCode: "+123456789");

        act.Should().Throw<ArgumentException>().WithParameterName("countryCode");
    }
}

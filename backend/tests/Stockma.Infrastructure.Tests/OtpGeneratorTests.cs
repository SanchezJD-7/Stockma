using FluentAssertions;
using Stockma.Infrastructure.Identity;

namespace Stockma.Infrastructure.Tests;

public class OtpGeneratorTests
{
    private readonly OtpGenerator generator = new();

    [Fact]
    public void Generate_ReturnsSixDigits()
    {
        generator.Generate().Should().HaveLength(OtpGenerator.Digits);
    }

    [Fact]
    public void Generate_ReturnsOnlyDigits()
    {
        generator.Generate().Should().MatchRegex("^[0-9]{6}$");
    }

    [Fact]
    public void Generate_PadsCodesBelowTheDigitCount()
    {
        Enumerable.Range(0, 2_000)
            .Select(_ => generator.Generate())
            .Should()
            .OnlyContain(code => code.Length == OtpGenerator.Digits);
    }

    [Fact]
    public void Generate_CoversTheWholeRange()
    {
        var codes = Enumerable.Range(0, 5_000).Select(_ => generator.Generate()).ToList();

        codes.Min(code => int.Parse(code))
            .Should()
            .BeLessThan(100_000, "si nunca aparece un código con cero inicial, el rango está sesgado");

        codes.Select(code => int.Parse(code))
            .Max()
            .Should()
            .BeGreaterThan(899_999, "el techo del rango también debe alcanzarse");
    }

    [Fact]
    public void Generate_DoesNotRepeatItselfPredictably()
    {
        var codes = Enumerable.Range(0, 1_000).Select(_ => generator.Generate()).ToHashSet();
        codes.Count
            .Should()
            .BeGreaterThan(990, "un generador sembrado o secuencial se delata acá");
    }

    [Fact]
    public void Generate_DoesNotDependOnInstanceState()
    {
        var first = new OtpGenerator().Generate();
        var second = new OtpGenerator().Generate();
        first.Should().NotBe(second);
    }
}

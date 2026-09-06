using FluentAssertions;
using Stockma.Domain.Enums;
using Stockma.Domain.Services;
using Stockma.Domain.ValueObjects;

namespace Stockma.Domain.Tests;

public class BatchStatusCalculatorTests
{
    private static readonly DateOnly Today = new(2026, 9, 2);

    [Fact]
    public void ExpiredWhenTheDateIsInThePast()
    {
        var color = BatchStatusCalculator.Evaluate(
            Today.AddDays(-1), Today, ExpiryThresholds.Default());

        color.Should().Be(SemaphoreColor.Expired);
    }

    [Fact]
    public void ExpiredIsExclusiveOfToday()
    {
        var color = BatchStatusCalculator.Evaluate(
            Today, Today, ExpiryThresholds.Default());

        color.Should().NotBe(SemaphoreColor.Expired);
        color.Should().Be(SemaphoreColor.Red, "vence hoy: está dentro del umbral rojo");
    }

    [Fact]
    public void RedWhenLessThanYellowMonthsRemain()
    {
        var color = BatchStatusCalculator.Evaluate(
            Today.AddMonths(2), Today, ExpiryThresholds.Default());

        color.Should().Be(SemaphoreColor.Red, "faltan menos de 3 meses");
    }

    [Fact]
    public void YellowBetweenYellowAndGreenMonths()
    {
        var color = BatchStatusCalculator.Evaluate(
            Today.AddMonths(4), Today, ExpiryThresholds.Default());

        color.Should().Be(SemaphoreColor.Yellow, "faltan entre 3 y 6 meses");
    }

    [Fact]
    public void GreenWhenMoreThanGreenMonthsRemain()
    {
        var color = BatchStatusCalculator.Evaluate(
            Today.AddMonths(7), Today, ExpiryThresholds.Default());

        color.Should().Be(SemaphoreColor.Green);
    }

    [Fact]
    public void CustomThresholdsChangeTheOutcome()
    {
        var thresholds = new ExpiryThresholds(greenMonths: 9, yellowMonths: 3);

        var color = BatchStatusCalculator.Evaluate(
            Today.AddMonths(7), Today, thresholds);

        color.Should().Be(SemaphoreColor.Yellow);
    }

    [Theory]
    [InlineData(3, SemaphoreColor.Yellow)]
    [InlineData(6, SemaphoreColor.Green)]
    public void ThresholdBoundariesBelongToTheLessUrgentColor(int months, SemaphoreColor expected)
    {
        var color = BatchStatusCalculator.Evaluate(
            Today.AddMonths(months), Today, ExpiryThresholds.Default());

        color.Should().Be(expected);
    }
}

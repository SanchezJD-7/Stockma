namespace Stockma.Domain.ValueObjects;

public sealed record ExpiryThresholds
{
    public const int DefaultGreenMonths = 6;
    public const int DefaultYellowMonths = 3;

    public ExpiryThresholds(int greenMonths, int yellowMonths)
    {
        if (yellowMonths <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(yellowMonths),
                yellowMonths,
                "YellowMonths debe ser mayor que 0.");
        }
        if (greenMonths <= yellowMonths)
        {
            throw new ArgumentOutOfRangeException(
                nameof(greenMonths),
                greenMonths,
                "GreenMonths debe ser mayor que YellowMonths.");
        }
        GreenMonths = greenMonths;
        YellowMonths = yellowMonths;
    }

    public int GreenMonths { get; private init; }
    public int YellowMonths { get; private init; }
    public static ExpiryThresholds Default() => new(DefaultGreenMonths, DefaultYellowMonths);
}

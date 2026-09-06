using Stockma.Domain.Enums;
using Stockma.Domain.ValueObjects;

namespace Stockma.Domain.Services;

public static class BatchStatusCalculator
{
    public static SemaphoreColor Evaluate(
        DateOnly expirationDate,
        DateOnly today,
        ExpiryThresholds thresholds)
    {
        ArgumentNullException.ThrowIfNull(thresholds);

        if (expirationDate < today)
        {
            return SemaphoreColor.Expired;
        }
        if (expirationDate < today.AddMonths(thresholds.YellowMonths))
        {
            return SemaphoreColor.Red;
        }
        if (expirationDate < today.AddMonths(thresholds.GreenMonths))
        {
            return SemaphoreColor.Yellow;
        }

        return SemaphoreColor.Green;
    }
}

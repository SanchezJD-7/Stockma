namespace Stockma.Infrastructure.Tests;

public sealed class FixedTimeProvider(DateOnly date) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() =>
        new(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
}

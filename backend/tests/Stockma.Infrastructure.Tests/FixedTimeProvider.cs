namespace Stockma.Infrastructure.Tests;

public sealed class FixedTimeProvider : TimeProvider
{
    private readonly DateTimeOffset instant;

    public FixedTimeProvider(DateOnly date)
        : this(new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero))
    {
    }

    public FixedTimeProvider(DateTimeOffset instant) => this.instant = instant;

    public override DateTimeOffset GetUtcNow() => instant;
}

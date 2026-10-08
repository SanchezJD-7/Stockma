namespace Stockma.Application.Identity;

public interface IDeviceOtpService
{
    Task IssueAsync(Guid userId, string deviceId, CancellationToken cancellationToken = default);

    Task<T> ConsumeAsync<T>(
        Guid userId,
        string deviceId,
        string code,
        Func<Task<T>> onConsumed,
        CancellationToken cancellationToken = default);

    Task InvalidateAllForUserAsync(Guid userId, DateTimeOffset invalidatedAt, CancellationToken cancellationToken = default);
}

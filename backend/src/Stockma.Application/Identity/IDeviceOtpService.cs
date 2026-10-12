namespace Stockma.Application.Identity;

public enum PhoneChangeIssueStatus{Issued,RateLimited,PendingExists}

public sealed record PhoneChangeIssueResult(PhoneChangeIssueStatus Status, int PendingExpiresInSeconds = 0)
{
    public bool Issued => Status == PhoneChangeIssueStatus.Issued;
}

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

    Task<PhoneChangeIssueResult> IssuePhoneNumberChangeAsync(Guid userId, string newPhoneNumber, CancellationToken cancellationToken = default);

    Task<T> ConsumePhoneNumberChangeAsync<T>(Guid userId, string code, Func<string, Task<T>> onConsumed, CancellationToken cancellationToken = default);
}

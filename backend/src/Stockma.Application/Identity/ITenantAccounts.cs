namespace Stockma.Application.Identity;

public interface ITenantAccounts
{
    Task<bool> ExistsAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<T> RunExclusivelyAsync<T>(Guid tenantId, Func<Task<T>> work, CancellationToken cancellationToken = default);
}

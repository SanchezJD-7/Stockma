using Microsoft.EntityFrameworkCore;
using Stockma.Application.Identity;
using Stockma.Infrastructure.Persistence;

namespace Stockma.Infrastructure.Tenants;

public sealed class TenantAccounts(StockmaDbContext context) : ITenantAccounts
{
    private const string LockScope = "tenant_bootstrap";

    public Task<bool> ExistsAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        context.Tenants.AnyAsync(tenant => tenant.Id == tenantId, cancellationToken);

    public Task<T> RunExclusivelyAsync<T>(Guid tenantId, Func<Task<T>> work, CancellationToken cancellationToken = default) =>
        context.RunLockedAsync(LockScope, tenantId, work, cancellationToken);
}

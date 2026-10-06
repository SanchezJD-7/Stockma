using Microsoft.EntityFrameworkCore;
using Stockma.Application.Identity;
using Stockma.Domain.Entities;
using Stockma.Infrastructure.Persistence;

namespace Stockma.Infrastructure.Tenants;

public sealed class TenantAccounts(StockmaDbContext context) : ITenantAccounts
{
    private const string LockScope = "tenant_bootstrap";

    public Task<bool> ExistsAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        context.Tenants.AnyAsync(tenant => tenant.Id == tenantId, cancellationToken);

    public async Task ProvisionAsync(
        Guid tenantId,
        bool requireSecondFactor,
        CancellationToken cancellationToken = default)
    {
        if (!await context.Tenants.AnyAsync(tenant => tenant.Id == tenantId, cancellationToken))
        {
            context.Tenants.Add(new Tenant(tenantId));
        }

        var settings = await context.TenantSettings
            .Where(candidate => candidate.TenantId == tenantId)
            .SingleOrDefaultAsync(cancellationToken);

        if (settings is null)
        {
            settings = TenantSettings.CreateDefault(tenantId);
            context.TenantSettings.Add(settings);
        }

        settings.SetRequireSecondFactor(requireSecondFactor);

        await context.SaveChangesAsync(cancellationToken);
    }

    public Task<T> RunExclusivelyAsync<T>(Guid tenantId, Func<Task<T>> work, CancellationToken cancellationToken = default) =>
        context.RunLockedAsync(LockScope, tenantId, work, cancellationToken);
}

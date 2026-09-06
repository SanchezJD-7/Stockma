using Stockma.Application.Common;

namespace Stockma.Infrastructure.Tenancy;

public sealed class TenantContext : ITenantContext
{
    private Guid? _tenantId;

    public Guid TenantId =>
        _tenantId ?? throw new InvalidOperationException(
            "El tenant no fue resuelto para esta request. TenantMiddleware debe ejecutarse antes de acceder a datos.");

    public bool IsResolved => _tenantId.HasValue;
    public void Set(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("El TenantId no puede ser Guid.Empty.", nameof(tenantId));
        }

        _tenantId = tenantId;
    }
}

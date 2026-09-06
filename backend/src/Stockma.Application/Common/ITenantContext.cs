namespace Stockma.Application.Common;

public interface ITenantContext
{
    Guid TenantId { get; }
    bool IsResolved { get; }
    void Set(Guid tenantId);
}

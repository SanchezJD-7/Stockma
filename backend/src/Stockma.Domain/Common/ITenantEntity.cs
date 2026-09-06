namespace Stockma.Domain.Common;

public interface ITenantEntity
{
    Guid TenantId { get; }
}

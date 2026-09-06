namespace Stockma.Domain.Entities;

public class Tenant
{
    private Tenant() { }

    public Tenant(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("El Id del tenant no puede ser Guid.Empty.", nameof(id));
        }

        Id = id;
    }

    public Guid Id { get; private set; }
    public TenantSettings? Settings { get; private set; }
}

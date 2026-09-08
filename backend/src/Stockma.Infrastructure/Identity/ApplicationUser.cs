using Microsoft.AspNetCore.Identity;
using Stockma.Domain.Common;

namespace Stockma.Infrastructure.Identity;

public class ApplicationUser : IdentityUser<Guid>, ITenantEntity
{
    public ApplicationUser()
    {
    }

    public ApplicationUser(Guid tenantId, string email)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("El TenantId no puede ser Guid.Empty.", nameof(tenantId));
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("El email es obligatorio.", nameof(email));
        }

        Id = Guid.CreateVersion7();
        TenantId = tenantId;
        Email = email.Trim();
        UserName = Email;
    }

    public Guid TenantId { get; private set; }
}

using Microsoft.AspNetCore.Identity;

namespace Stockma.Infrastructure.Identity;

public class ApplicationRole : IdentityRole<Guid>
{
    public ApplicationRole()
    {
    }

    public ApplicationRole(string name)
        : base(name) => Id = Guid.CreateVersion7();
}

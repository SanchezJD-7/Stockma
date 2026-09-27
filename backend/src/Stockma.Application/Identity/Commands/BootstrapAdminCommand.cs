using MediatR;
using Stockma.Application.Common;
using Stockma.Domain.Exceptions;

namespace Stockma.Application.Identity.Commands;

public sealed record BootstrapAdminCommand(
    Guid TenantId,
    string Email,
    string Password,
    string PhoneNumber) : IRequest<RegisteredUser>;

public sealed class BootstrapAdminCommandHandler(
    ITenantAccounts tenants,
    IUserAccounts accounts,
    ITenantContext tenantContext) : IRequestHandler<BootstrapAdminCommand, RegisteredUser>
{
    public async Task<RegisteredUser> Handle(BootstrapAdminCommand command, CancellationToken cancellationToken)
    {
        if (!await tenants.ExistsAsync(command.TenantId, cancellationToken))
        {
            throw new TenantNotFoundException(command.TenantId);
        }

        tenantContext.Set(command.TenantId);

        return await tenants.RunExclusivelyAsync(
            command.TenantId,
            async () =>
            {
                if (await accounts.TenantHasAnyUserAsync(cancellationToken))
                {
                    throw new TenantAlreadyBootstrappedException(command.TenantId);
                }

                var register = new RegisterUserCommandHandler(accounts, tenantContext);

                return await register.Handle(
                    new RegisterUserCommand(command.Email, command.Password, command.PhoneNumber, TenantRoles.TenantAdmin),
                    cancellationToken);
            },
            cancellationToken);
    }
}

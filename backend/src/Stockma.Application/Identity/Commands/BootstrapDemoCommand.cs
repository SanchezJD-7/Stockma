using MediatR;
using Stockma.Application.Common;
using Stockma.Domain.Exceptions;

namespace Stockma.Application.Identity.Commands;

public sealed record BootstrapDemoCommand(
    Guid TenantId,
    string Email,
    string Password,
    string PhoneNumber) : IRequest<RegisteredUser>;

public sealed class BootstrapDemoCommandHandler(
    ITenantAccounts tenants,
    IUserAccounts accounts,
    ITenantContext tenantContext) : IRequestHandler<BootstrapDemoCommand, RegisteredUser>
{
    public async Task<RegisteredUser> Handle(BootstrapDemoCommand command, CancellationToken cancellationToken)
    {
        tenantContext.Set(command.TenantId);

        await tenants.ProvisionAsync(command.TenantId, requireSecondFactor: false, cancellationToken);

        var existing = await accounts.FindByEmailAsync(command.Email, cancellationToken);

        if (existing is not null)
        {
            if (existing.TenantId != command.TenantId)
            {
                throw new EmailAlreadyRegisteredException();
            }

            return new RegisteredUser(existing.UserId, command.Email);
        }

        var register = new RegisterUserCommandHandler(accounts, tenantContext);

        return await register.Handle(
            new RegisterUserCommand(command.Email, command.Password, command.PhoneNumber, TenantRoles.Member),
            cancellationToken);
    }
}

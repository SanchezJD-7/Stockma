using MediatR;
using Stockma.Domain.Exceptions;

namespace Stockma.Application.Identity.Commands;

public sealed record SetUserEnabledCommand(Guid UserId, bool Enabled) : IRequest<SetUserEnabledResult>;

public sealed record SetUserEnabledResult(Guid UserId, bool Enabled);

public sealed class SetUserEnabledCommandHandler(
    IUserAccounts accounts,
    IRefreshTokens refreshTokens,
    ITrustedDevices trustedDevices,
    TimeProvider timeProvider) : IRequestHandler<SetUserEnabledCommand, SetUserEnabledResult>
{
    public async Task<SetUserEnabledResult> Handle(SetUserEnabledCommand command, CancellationToken cancellationToken)
    {
        var role = await accounts.GetRoleAsync(command.UserId, cancellationToken);

        if (!command.Enabled)
        {
            if (role == TenantRoles.TenantAdmin
                && await accounts.CountOtherTenantAdminsAsync(command.UserId, cancellationToken) == 0)
            {
                throw new ValidationFailedException(
                    "No se puede deshabilitar al último TenantAdmin del tenant: quedaría sin quien administre.");
            }

            await trustedDevices.RevokeAsync(command.UserId, null, cancellationToken);
            await refreshTokens.RevokeAllForUserAsync(command.UserId, timeProvider.GetUtcNow(), cancellationToken);
        }

        await accounts.SetAccountEnabledAsync(command.UserId, command.Enabled, cancellationToken);
        return new SetUserEnabledResult(command.UserId, command.Enabled);
    }
}

using MediatR;
using Stockma.Domain.Exceptions;

namespace Stockma.Application.Identity.Commands;

public sealed record SetUserRoleCommand(Guid UserId, string Role) : IRequest<SetUserRoleResult>;

public sealed record SetUserRoleResult(Guid UserId, string Role);

public sealed class SetUserRoleCommandHandler(
    IUserAccounts accounts,
    IRefreshTokens refreshTokens,
    ITrustedDevices trustedDevices,
    TimeProvider timeProvider) : IRequestHandler<SetUserRoleCommand, SetUserRoleResult>
{
    public async Task<SetUserRoleResult> Handle(
        SetUserRoleCommand command,
        CancellationToken cancellationToken)
    {
        var role = command.Role?.Trim() ?? string.Empty;

        if (!TenantRoles.IsKnown(role))
        {
            throw new ValidationFailedException(
                $"'{command.Role}' no es un rol de tenant. Valores admitidos: {string.Join(", ", TenantRoles.All)}.");
        }

        var currentRole = await accounts.GetRoleAsync(command.UserId, cancellationToken);

        if (currentRole == role)
        {
            return new SetUserRoleResult(command.UserId, role);
        }

        var demoting = currentRole == TenantRoles.TenantAdmin && role == TenantRoles.Member;

        if (demoting)
        {
            if (await accounts.CountOtherTenantAdminsAsync(command.UserId, cancellationToken) == 0)
            {
                throw new ValidationFailedException(
                    "No se puede degradar al último TenantAdmin del tenant: quedaría sin quien administre.");
            }

            // Orden fail-closed (misma lógica que T050): revocar ANTES de escribir el rol.
            // El rol vive dentro del JWT emitido, así que si algo falla a mitad de camino
            // la persona queda MENOS privilegiada (sin sesión), nunca más.
            await refreshTokens.RevokeAllForUserAsync(command.UserId, timeProvider.GetUtcNow(), cancellationToken);
            await trustedDevices.RevokeAsync(command.UserId, null, cancellationToken);
        }

        await accounts.SetRoleAsync(command.UserId, role, cancellationToken);

        return new SetUserRoleResult(command.UserId, role);
    }
}

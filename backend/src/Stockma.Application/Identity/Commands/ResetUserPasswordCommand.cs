using MediatR;

namespace Stockma.Application.Identity.Commands;

/// <summary>
/// T069 — fallback admin: para quien perdió el celular Y la contraseña, sin esto queda
/// encerrado afuera. Cambia la contraseña directamente (sin OTP), corta todas las
/// familias de refresh y invalida los OTPs vivos. NO toca LockoutEnd: deshabilitar
/// (T068) sigue siendo una acción explícita aparte — un reset no rehabilita.
/// </summary>
public sealed record ResetUserPasswordCommand(Guid UserId, string NewPassword) : IRequest<ResetUserPasswordResult>;

public sealed record ResetUserPasswordResult(Guid UserId);

public sealed class ResetUserPasswordCommandHandler(
    IUserAccounts accounts,
    IDeviceOtpService deviceOtps,
    IRefreshTokens refreshTokens,
    TimeProvider timeProvider) : IRequestHandler<ResetUserPasswordCommand, ResetUserPasswordResult>
{
    public async Task<ResetUserPasswordResult> Handle(ResetUserPasswordCommand command, CancellationToken cancellationToken)
    {
        await accounts.GetRoleAsync(command.UserId, cancellationToken);
        await refreshTokens.RevokeAllForUserAsync(command.UserId, timeProvider.GetUtcNow(), cancellationToken);
        await deviceOtps.InvalidateAllForUserAsync(command.UserId, timeProvider.GetUtcNow(), cancellationToken);
        await accounts.SetPasswordAsync(command.UserId, command.NewPassword, cancellationToken);

        return new ResetUserPasswordResult(command.UserId);
    }
}

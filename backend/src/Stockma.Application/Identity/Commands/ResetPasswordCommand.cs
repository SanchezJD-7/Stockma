using MediatR;
using Stockma.Application.Common;
using Stockma.Domain.Exceptions;

namespace Stockma.Application.Identity.Commands;

public sealed record ResetPasswordCommand(string Email, string Code, string NewPassword) : IRequest;

public sealed class ResetPasswordCommandHandler(
    IUserAccounts accounts,
    IDeviceOtpService deviceOtps,
    IRefreshTokens refreshTokens,
    ITenantContext tenantContext,
    TimeProvider timeProvider) : IRequestHandler<ResetPasswordCommand>
{
    public async Task Handle(ResetPasswordCommand command, CancellationToken cancellationToken)
    {
        var identity = await accounts.FindByEmailAsync(command.Email, cancellationToken) ?? throw new OtpNotUsableException();

        tenantContext.Set(identity.TenantId);

        await deviceOtps.ConsumePasswordResetAsync(identity.UserId, command.Code,
            async () =>
            {
                await accounts.SetPasswordAsync(identity.UserId, command.NewPassword, cancellationToken);
                await refreshTokens.RevokeAllForUserAsync(identity.UserId, timeProvider.GetUtcNow(), cancellationToken);
                await deviceOtps.InvalidateAllForUserAsync(identity.UserId, timeProvider.GetUtcNow(), cancellationToken);
                return true;
            },
            cancellationToken);
    }
}

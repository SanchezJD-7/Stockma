using MediatR;
using Microsoft.Extensions.Logging;
using Stockma.Domain.Exceptions;
using Stockma.Domain.ValueObjects;

namespace Stockma.Application.Identity.Commands;

public sealed record ConfirmPhoneNumberChangeCommand(Guid UserId, string Otp)
    : IRequest<ConfirmPhoneNumberChangeResult>;

public sealed record ConfirmPhoneNumberChangeResult(string PhoneNumberMasked, bool PhoneNumberConfirmed);

public sealed class ConfirmPhoneNumberChangeCommandHandler(
    IUserAccounts accounts,
    IDeviceOtpService deviceOtps,
    IRefreshTokens refreshTokens,
    ITrustedDevices trustedDevices,
    ISmsSender smsSender,
    TimeProvider timeProvider,
    ILogger<ConfirmPhoneNumberChangeCommandHandler> logger)
    : IRequestHandler<ConfirmPhoneNumberChangeCommand, ConfirmPhoneNumberChangeResult>
{
    public async Task<ConfirmPhoneNumberChangeResult> Handle(ConfirmPhoneNumberChangeCommand command, CancellationToken cancellationToken)
    {
        var oldPhoneNumber = await accounts.GetUserPhoneNumberAsync(command.UserId, cancellationToken) ?? throw new PhoneNotEnrolledException();

        if (await accounts.FindByIdAsync(command.UserId, cancellationToken) is null) throw new AccountDisabledException();

        var masked = await deviceOtps.ConsumePhoneNumberChangeAsync(command.UserId, command.Otp, async newPhoneNumber =>
            {
                await trustedDevices.RevokeAsync(command.UserId, null, cancellationToken);
                await refreshTokens.RevokeAllForUserAsync(command.UserId, timeProvider.GetUtcNow(), cancellationToken);
                await deviceOtps.InvalidateAllForUserAsync(command.UserId, timeProvider.GetUtcNow(), cancellationToken);
                await accounts.SetPhoneNumberAsync(command.UserId, newPhoneNumber, cancellationToken);
                return PhoneNumber.Mask(newPhoneNumber);
            },
            cancellationToken);

        try
        {
            await smsSender.SendAsync(oldPhoneNumber, "Tu número de Stockma fue cambiado. Si no fuiste tu notificale de inmediato al admin de tu organización.", cancellationToken);
        }
        catch (SmsDeliveryException exception)
        {
            logger.LogWarning(exception, "Falló el aviso de cambio de número al número viejo del usuario {UserId}.", command.UserId);
        }

        return new ConfirmPhoneNumberChangeResult(masked, PhoneNumberConfirmed: true);
    }
}

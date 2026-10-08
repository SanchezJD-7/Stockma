using MediatR;
using Stockma.Domain.ValueObjects;

namespace Stockma.Application.Identity.Commands;

public sealed record SetUserPhoneNumberCommand(Guid UserId, string PhoneNumber)
    : IRequest<SetUserPhoneNumberResult>;

public sealed record SetUserPhoneNumberResult(Guid UserId, string PhoneNumberMasked, bool PhoneNumberConfirmed);

public sealed class SetUserPhoneNumberCommandHandler(
    IUserAccounts accounts,
    IRefreshTokens refreshTokens,
    ITrustedDevices trustedDevices,
    IDeviceOtpService deviceOtps,
    TimeProvider timeProvider) : IRequestHandler<SetUserPhoneNumberCommand, SetUserPhoneNumberResult>
{
    public async Task<SetUserPhoneNumberResult> Handle(
        SetUserPhoneNumberCommand command,
        CancellationToken cancellationToken)
    {
        var phoneNumber = PhoneNumber.Parse(command.PhoneNumber);

        await trustedDevices.RevokeAsync(command.UserId, null, cancellationToken);
        await refreshTokens.RevokeAllForUserAsync(command.UserId, timeProvider.GetUtcNow(), cancellationToken);
        await deviceOtps.InvalidateAllForUserAsync(command.UserId, timeProvider.GetUtcNow(), cancellationToken);
        await accounts.SetPhoneNumberAsync(command.UserId, phoneNumber, cancellationToken);

        return new SetUserPhoneNumberResult(command.UserId, PhoneNumber.Mask(phoneNumber), PhoneNumberConfirmed: true);
    }
}

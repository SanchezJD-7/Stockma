using MediatR;
using Stockma.Application.Common;
using Stockma.Domain.Exceptions;

namespace Stockma.Application.Identity.Commands;

public sealed record ConfirmDeviceCommand(
    string Email,
    string DeviceId,
    string Fingerprint,
    string Otp) : IRequest<ConfirmDeviceResult>;

public sealed class ConfirmDeviceCommandHandler(
    IUserAccounts accounts,
    ITrustedDevices trustedDevices,
    IDeviceOtpService deviceOtps,
    IJwtTokenService tokens,
    ITenantContext tenantContext) : IRequestHandler<ConfirmDeviceCommand, ConfirmDeviceResult>
{
    public async Task<ConfirmDeviceResult> Handle(
        ConfirmDeviceCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.DeviceId))
        {
            throw new ArgumentException("El identificador del dispositivo es obligatorio.", nameof(command.DeviceId));
        }

        var identity = await accounts.FindByEmailAsync(command.Email, cancellationToken)
            ?? throw new InvalidCredentialsException();

        tenantContext.Set(identity.TenantId);

        await deviceOtps.ConsumeAsync(identity.UserId, command.DeviceId, command.Otp, cancellationToken);

        var trusted = await trustedDevices.TryTrustAsync(
            identity.UserId,
            command.DeviceId,
            command.Fingerprint,
            cancellationToken);

        var token = tokens.Create(identity.UserId, identity.TenantId, identity.Roles);

        return new ConfirmDeviceResult(token.Value, token.ExpiresInSeconds, trusted);
    }
}

using MediatR;
using Stockma.Application.Common;
using Stockma.Domain.Entities;
using Stockma.Domain.Exceptions;
using Stockma.Domain.ValueObjects;

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
        var deviceId = DeviceIdentifier.Parse(command.DeviceId);
        var fingerprint = ParseFingerprint(command.Fingerprint);

        var identity = await accounts.FindByEmailAsync(command.Email, cancellationToken)
            ?? throw new OtpNotUsableException();

        tenantContext.Set(identity.TenantId);

        return await deviceOtps.ConsumeAsync(
            identity.UserId,
            deviceId,
            command.Otp,
            async () =>
            {
                var trusted = await trustedDevices.TryTrustAsync(identity.UserId, deviceId, fingerprint, cancellationToken);
                var token = tokens.Create(identity.UserId, identity.TenantId, identity.Roles);

                return new ConfirmDeviceResult(token.Value, token.ExpiresInSeconds, trusted);
            },
            cancellationToken);
    }

    private static string ParseFingerprint(string? fingerprint)
    {
        var normalized = fingerprint?.Trim() ?? string.Empty;

        if (normalized.Length == 0 || normalized.Length > TrustedDevice.MaxFingerprintLength)
        {
            throw new ValidationFailedException(
                $"La huella del dispositivo es obligatoria y no puede superar {TrustedDevice.MaxFingerprintLength} caracteres.");
        }

        return normalized;
    }
}

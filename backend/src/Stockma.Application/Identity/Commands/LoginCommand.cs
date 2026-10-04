using MediatR;
using Stockma.Application.Common;
using Stockma.Domain.Exceptions;
using Stockma.Domain.ValueObjects;

namespace Stockma.Application.Identity.Commands;

public sealed record LoginCommand(
    string Email,
    string Password,
    string? DeviceId) : IRequest<LoginResult>;

public sealed class LoginCommandHandler(
    IUserAccounts accounts,
    ITrustedDevices trustedDevices,
    IDeviceOtpService deviceOtps,
    IJwtTokenService tokens,
    IRefreshTokenService sessions,
    ITenantContext tenantContext) : IRequestHandler<LoginCommand, LoginResult>
{
    public async Task<LoginResult> Handle(LoginCommand command, CancellationToken cancellationToken)
    {
        var deviceId = DeviceIdentifier.Parse(command.DeviceId);

        var identity = await accounts.VerifyCredentialsAsync(command.Email, command.Password, cancellationToken)
            ?? throw new InvalidCredentialsException();

        tenantContext.Set(identity.TenantId);

        if (await trustedDevices.IsTrustedAsync(identity.UserId, deviceId, cancellationToken))
        {
            var token = tokens.Create(identity.UserId, identity.TenantId, identity.Roles);
            var refreshToken = await sessions.IssueNewFamilyAsync(
                identity.TenantId,
                identity.UserId,
                deviceId,
                cancellationToken);

            return LoginResult.Issued(token, refreshToken);
        }

        await deviceOtps.IssueAsync(identity.UserId, deviceId, cancellationToken);

        return LoginResult.NeedsDeviceConfirmation();
    }
}

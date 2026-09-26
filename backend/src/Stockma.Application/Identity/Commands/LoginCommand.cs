using MediatR;
using Stockma.Application.Common;
using Stockma.Domain.Exceptions;

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
    ITenantContext tenantContext) : IRequestHandler<LoginCommand, LoginResult>
{
    public async Task<LoginResult> Handle(LoginCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.DeviceId))
        {
            throw new ArgumentException("El identificador del dispositivo es obligatorio en el login.", nameof(command.DeviceId));
        }

        var identity = await accounts.VerifyCredentialsAsync(command.Email, command.Password, cancellationToken)
            ?? throw new InvalidCredentialsException();

        tenantContext.Set(identity.TenantId);

        if (await trustedDevices.IsTrustedAsync(identity.UserId, command.DeviceId, cancellationToken))
        {
            return LoginResult.Issued(tokens.Create(identity.UserId, identity.TenantId, identity.Roles));
        }

        await deviceOtps.IssueAsync(identity.UserId, command.DeviceId, cancellationToken);

        return LoginResult.NeedsDeviceConfirmation();
    }
}

using MediatR;
using Stockma.Application.Common;
using Stockma.Domain.Exceptions;

namespace Stockma.Application.Identity.Commands;

public sealed record RequestPasswordResetCommand(string Email) : IRequest;

public sealed class RequestPasswordResetCommandHandler(
    IUserAccounts accounts,
    IDeviceOtpService deviceOtps,
    ITenantContext tenantContext) : IRequestHandler<RequestPasswordResetCommand>
{
    public async Task Handle(RequestPasswordResetCommand command, CancellationToken cancellationToken)
    {
        var identity = await accounts.FindByEmailAsync(command.Email, cancellationToken);

        if (identity is null) return;

        tenantContext.Set(identity.TenantId);

        try
        {
            await deviceOtps.IssuePasswordResetAsync(identity.UserId, cancellationToken);
        }
        catch (UserNotFoundException)
        {
        }
        catch (PhoneNotEnrolledException)
        {
        }
    }
}

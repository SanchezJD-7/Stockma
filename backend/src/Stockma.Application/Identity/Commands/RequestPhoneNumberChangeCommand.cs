using MediatR;
using Stockma.Application.Tenants;
using Stockma.Domain.Entities;
using Stockma.Domain.Exceptions;
using Stockma.Domain.ValueObjects;

namespace Stockma.Application.Identity.Commands;

public sealed record RequestPhoneNumberChangeCommand(Guid UserId, string NewPhoneNumber) : IRequest<RequestPhoneNumberChangeResult>;

public sealed record RequestPhoneNumberChangeResult(string OtpSentToMasked, int ExpiresInSeconds);

public sealed class RequestPhoneNumberChangeCommandHandler(
    IUserAccounts accounts,
    IDeviceOtpService deviceOtps,
    ITenantSettingsProvider tenantSettings) : IRequestHandler<RequestPhoneNumberChangeCommand, RequestPhoneNumberChangeResult>
{
    public async Task<RequestPhoneNumberChangeResult> Handle(RequestPhoneNumberChangeCommand command, CancellationToken cancellationToken)
    {
        var newPhoneNumber = PhoneNumber.Parse(command.NewPhoneNumber);
        CountryPhoneRules.Validate(await tenantSettings.GetCountryCodeAsync(cancellationToken), newPhoneNumber);
        var currentPhoneNumber = await accounts.GetUserPhoneNumberAsync(command.UserId, cancellationToken) ?? throw new PhoneNotEnrolledException();
        if (currentPhoneNumber == newPhoneNumber)
        {
            throw new ValidationFailedException("El número pedido y el actual son los mismos.");
        }

        var issue = await deviceOtps.IssuePhoneNumberChangeAsync(command.UserId, newPhoneNumber, cancellationToken);

        if (issue.Status == PhoneChangeIssueStatus.PendingExists)
        {
            throw new PhoneChangePendingException(issue.PendingExpiresInSeconds);
        }

        return new RequestPhoneNumberChangeResult(PhoneNumber.Mask(currentPhoneNumber), (int)DeviceOtp.MaxLifetime.TotalSeconds);
    }
}

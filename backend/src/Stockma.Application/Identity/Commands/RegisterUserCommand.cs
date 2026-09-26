using MediatR;
using Stockma.Application.Common;
using Stockma.Domain.Exceptions;

namespace Stockma.Application.Identity.Commands;

public sealed record RegisterUserCommand(
    string Email,
    string Password,
    string PhoneNumber,
    string Role = TenantRoles.Member) : IRequest<RegisteredUser>;

public sealed class RegisterUserCommandHandler(
    IUserAccounts accounts,
    ITenantContext tenantContext) : IRequestHandler<RegisterUserCommand, RegisteredUser>
{
    public async Task<RegisteredUser> Handle(RegisterUserCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Email))
        {
            throw new ArgumentException("El email es obligatorio.", nameof(command.Email));
        }

        if (string.IsNullOrWhiteSpace(command.PhoneNumber))
        {
            throw new ArgumentException(
                "El celular es obligatorio en el alta: sin el, el usuario no puede recibir el OTP.",
                nameof(command.PhoneNumber));
        }

        if (!TenantRoles.IsKnown(command.Role))
        {
            throw new ArgumentException(
                $"'{command.Role}' no es un rol de tenant. Valores admitidos: {string.Join(", ", TenantRoles.All)}.",
                nameof(command.Role));
        }

        var email = command.Email.Trim();

        if (await accounts.EmailExistsAsync(email, cancellationToken))
        {
            throw new EmailAlreadyRegisteredException();
        }

        var userId = await accounts.CreateAsync(
            new NewUser(tenantContext.TenantId, email, command.Password, command.PhoneNumber.Trim(), command.Role),
            cancellationToken);

        return new RegisteredUser(userId, email);
    }
}

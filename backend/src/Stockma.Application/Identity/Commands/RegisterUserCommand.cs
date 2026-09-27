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
            throw new ValidationFailedException("El email es obligatorio.");
        }

        if (string.IsNullOrWhiteSpace(command.PhoneNumber))
        {
            throw new ValidationFailedException(
                "El celular es obligatorio en el alta: sin el, el usuario no puede recibir el OTP.");
        }

        if (!TenantRoles.IsKnown(command.Role))
        {
            throw new ValidationFailedException(
                $"'{command.Role}' no es un rol de tenant. Valores admitidos: {string.Join(", ", TenantRoles.All)}.");
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

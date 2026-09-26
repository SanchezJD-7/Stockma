namespace Stockma.Application.Identity;

public interface IUserAccounts
{
    Task<LoginIdentity?> VerifyCredentialsAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default);

    Task<LoginIdentity?> FindByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<Guid> CreateAsync(NewUser user, CancellationToken cancellationToken = default);

    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);
}

public sealed record LoginIdentity(Guid UserId, Guid TenantId, IReadOnlyList<string> Roles);

public sealed record NewUser(Guid TenantId, string Email, string Password, string PhoneNumber, string Role);

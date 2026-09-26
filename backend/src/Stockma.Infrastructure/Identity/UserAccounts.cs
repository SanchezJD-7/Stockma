using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Stockma.Application.Identity;
using Stockma.Infrastructure.Persistence;

namespace Stockma.Infrastructure.Identity;

public sealed class UserAccounts(
    StockmaDbContext context,
    UserManager<ApplicationUser> userManager,
    ILookupNormalizer lookupNormalizer,
    IPasswordHasher<ApplicationUser> passwordHasher,
    TimeProvider timeProvider) : IUserAccounts
{
    private const string FindByEmailSql =
        """
        SELECT id, tenant_id, password_hash, security_stamp, lockout_end, lockout_enabled
          FROM auth_find_user_by_email(@email);
        """;

    private sealed record LoginCandidate(
        Guid UserId,
        Guid TenantId,
        string? PasswordHash,
        DateTimeOffset? LockoutEnd,
        bool LockoutEnabled);

    public async Task<LoginIdentity?> VerifyCredentialsAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        var candidate = await FindCandidateAsync(email, cancellationToken);

        if (candidate is null || string.IsNullOrEmpty(candidate.PasswordHash))
        {
            return null;
        }

        if (IsLockedOut(candidate))
        {
            return null;
        }

        var verification = passwordHasher.VerifyHashedPassword(
            new ApplicationUser(),
            candidate.PasswordHash,
            password);

        if (verification == PasswordVerificationResult.Failed)
        {
            return null;
        }

        return await ToIdentityAsync(candidate, cancellationToken);
    }

    public async Task<LoginIdentity?> FindByEmailAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        var candidate = await FindCandidateAsync(email, cancellationToken);

        return candidate is null ? null : await ToIdentityAsync(candidate, cancellationToken);
    }

    public async Task<Guid> CreateAsync(NewUser newUser, CancellationToken cancellationToken = default)
    {
        var user = new ApplicationUser(newUser.TenantId, newUser.Email)
        {
            PhoneNumber = newUser.PhoneNumber,
        };

        var created = await userManager.CreateAsync(user, newUser.Password);

        if (!created.Succeeded)
        {
            throw new InvalidOperationException(
                $"No se pudo crear el usuario: {string.Join("; ", created.Errors.Select(e => e.Description))}");
        }

        var assigned = await userManager.AddToRoleAsync(user, newUser.Role);

        if (!assigned.Succeeded)
        {
            throw new InvalidOperationException(
                $"No se pudo asignar el rol '{newUser.Role}': "
                + string.Join("; ", assigned.Errors.Select(e => e.Description)));
        }

        return user.Id;
    }

    public async Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default) =>
        await FindCandidateAsync(email, cancellationToken) is not null;

    private async Task<LoginCandidate?> FindCandidateAsync(string email, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var normalized = lookupNormalizer.NormalizeEmail(email.Trim());

        var connection = context.Database.GetDbConnection();
        var wasClosed = connection.State != System.Data.ConnectionState.Open;

        if (wasClosed)
        {
            await context.Database.OpenConnectionAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = FindByEmailSql;
            command.Parameters.Add(new NpgsqlParameter("email", normalized));

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            return new LoginCandidate(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.IsDBNull(4) ? null : reader.GetFieldValue<DateTimeOffset>(4),
                reader.GetBoolean(5));
        }
        finally
        {
            if (wasClosed)
            {
                await context.Database.CloseConnectionAsync();
            }
        }
    }

    private bool IsLockedOut(LoginCandidate candidate) =>
        candidate.LockoutEnabled
        && candidate.LockoutEnd is { } until
        && until > timeProvider.GetUtcNow();

    private async Task<LoginIdentity> ToIdentityAsync(
        LoginCandidate candidate,
        CancellationToken cancellationToken)
    {
        var roles = await context.UserRoles
            .Where(userRole => userRole.UserId == candidate.UserId)
            .Join(context.Roles, userRole => userRole.RoleId, role => role.Id, (_, role) => role.Name!)
            .ToListAsync(cancellationToken);

        return new LoginIdentity(candidate.UserId, candidate.TenantId, roles);
    }
}

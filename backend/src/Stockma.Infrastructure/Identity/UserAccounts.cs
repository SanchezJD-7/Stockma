using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Stockma.Application.Identity;
using Stockma.Domain.Exceptions;
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

    private const string NormalizedEmailIndex = "ux_users_normalized_email";
    private const string NormalizedUserNameIndex = "ux_users_normalized_user_name";

    private static readonly string[] DuplicateEmailErrorCodes =
    [
        nameof(IdentityErrorDescriber.DuplicateEmail),
        nameof(IdentityErrorDescriber.DuplicateUserName),
    ];

    private static readonly string DummyPasswordHash =
        new PasswordHasher<ApplicationUser>().HashPassword(new ApplicationUser(), Guid.NewGuid().ToString());

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

        if (candidate is null || string.IsNullOrEmpty(candidate.PasswordHash) || IsLockedOut(candidate))
        {
            passwordHasher.VerifyHashedPassword(new ApplicationUser(), DummyPasswordHash, password);
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

        return candidate is null || IsLockedOut(candidate) ? null : await ToIdentityAsync(candidate, cancellationToken);
    }

    public async Task<Guid> CreateAsync(NewUser newUser, CancellationToken cancellationToken = default)
    {
        if (context.Database.CurrentTransaction is not null)
        {
            return await CreateWithRoleAsync(newUser);
        }

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var userId = await CreateWithRoleAsync(newUser);
            await transaction.CommitAsync(cancellationToken);
            return userId;
        }
        catch
        {
            context.ChangeTracker.Clear();
            throw;
        }
    }

    private async Task<Guid> CreateWithRoleAsync(NewUser newUser)
    {
        var user = new ApplicationUser(newUser.TenantId, newUser.Email)
        {
            PhoneNumber = newUser.PhoneNumber,
        };

        IdentityResult created;

        try
        {
            created = await userManager.CreateAsync(user, newUser.Password);
        }
        catch (DbUpdateException exception) when (IsDuplicateEmail(exception))
        {
            throw new EmailAlreadyRegisteredException();
        }

        if (created.Errors.Any(error => DuplicateEmailErrorCodes.Contains(error.Code)))
        {
            throw new EmailAlreadyRegisteredException();
        }

        if (!created.Succeeded)
        {
            throw new ValidationFailedException(
                "El email o la contraseña no cumplen las reglas de alta: "
                + string.Join(", ", created.Errors.Select(e => e.Code)));
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

    private static bool IsDuplicateEmail(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: NormalizedEmailIndex or NormalizedUserNameIndex,
        };

    public async Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default) =>
        await FindCandidateAsync(email, cancellationToken) is not null;

    public Task<bool> TenantHasAnyUserAsync(CancellationToken cancellationToken = default) =>
        context.Users.AnyAsync(cancellationToken);

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

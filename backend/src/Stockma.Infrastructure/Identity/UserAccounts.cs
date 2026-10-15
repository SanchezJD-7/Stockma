using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using Stockma.Application.Common;
using Stockma.Application.Identity;
using Stockma.Domain.Exceptions;
using Stockma.Infrastructure.Persistence;

namespace Stockma.Infrastructure.Identity;

public sealed class UserAccounts(
    StockmaDbContext context,
    ITenantContext tenantContext,
    UserManager<ApplicationUser> userManager,
    IPasswordValidator<ApplicationUser> passwordValidator,
    ILookupNormalizer lookupNormalizer,
    IPasswordHasher<ApplicationUser> passwordHasher,
    TimeProvider timeProvider,
    IOptions<IdentityOptions> identityOptions) : IUserAccounts
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

        if (candidate is null || string.IsNullOrEmpty(candidate.PasswordHash)
            || IsLockedOut(candidate.LockoutEnabled, candidate.LockoutEnd))
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
            await RecordFailedAccessAsync(candidate, cancellationToken);
            return null;
        }

        await ResetFailedAccessAsync(candidate, cancellationToken);

        return await ToIdentityAsync(candidate.UserId, candidate.TenantId, cancellationToken);
    }

    private async Task RecordFailedAccessAsync(LoginCandidate candidate, CancellationToken cancellationToken)
    {
        await ActivateTenantAsync(candidate.TenantId, cancellationToken);

        var user = await context.Users.FirstOrDefaultAsync(
            entity => entity.Id == candidate.UserId, cancellationToken);

        if (user is null) return;

        user.AccessFailedCount++;

        if (user.AccessFailedCount >= identityOptions.Value.Lockout.MaxFailedAccessAttempts)
        {
            user.LockoutEnd = timeProvider.GetUtcNow() + identityOptions.Value.Lockout.DefaultLockoutTimeSpan;
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task ResetFailedAccessAsync(LoginCandidate candidate, CancellationToken cancellationToken)
    {
        await ActivateTenantAsync(candidate.TenantId, cancellationToken);

        var user = await context.Users.FirstOrDefaultAsync(
            entity => entity.Id == candidate.UserId, cancellationToken);

        if (user is null || user.AccessFailedCount == 0)
        {
            return;
        }

        user.AccessFailedCount = 0;
        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task ActivateTenantAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        tenantContext.Set(tenantId);

        var connection = context.Database.GetDbConnection();

        if (connection.State != System.Data.ConnectionState.Open)
        {
            return;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT set_config('app.tenant', @tenant, false);";

        var parameter = command.CreateParameter();
        parameter.ParameterName = "tenant";
        parameter.Value = tenantId.ToString();
        command.Parameters.Add(parameter);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<LoginIdentity?> FindByEmailAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        var candidate = await FindCandidateAsync(email, cancellationToken);

        return candidate is null || IsLockedOut(candidate.LockoutEnabled, candidate.LockoutEnd)
            ? null
            : await ToIdentityAsync(candidate.UserId, candidate.TenantId, cancellationToken);
    }

    public async Task<LoginIdentity?> FindByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await context.Users.FirstOrDefaultAsync(user => user.Id == userId, cancellationToken);

        return user is null || IsLockedOut(user.LockoutEnabled, user.LockoutEnd)
            ? null
            : await ToIdentityAsync(user.Id, user.TenantId, cancellationToken);
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

    public async Task SetPhoneNumberAsync(
        Guid userId,
        string phoneNumber,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new UserNotFoundException(userId);

        if (user.TenantId != context.CurrentTenantId)
        {
            throw new UserNotFoundException(userId);
        }

        user.PhoneNumber = phoneNumber;
        user.PhoneNumberConfirmed = true;

        var updated = await userManager.UpdateAsync(user);

        if (!updated.Succeeded)
        {
            throw new ValidationFailedException(
                "No se pudo guardar el número: " + string.Join(", ", updated.Errors.Select(error => error.Code)));
        }
    }

    public async Task SetAccountEnabledAsync(Guid userId, bool enabled, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString()) ?? throw new UserNotFoundException(userId);

        if (user.TenantId != context.CurrentTenantId) throw new UserNotFoundException(userId);

        user.LockoutEnabled = true;
        user.LockoutEnd = enabled ? null : DateTimeOffset.MaxValue;
        user.AccessFailedCount = 0;

        var updated = await userManager.UpdateAsync(user);

        if (!updated.Succeeded)
        {
            throw new ValidationFailedException(
                "No se pudo guardar el estado de la cuenta: "
                + string.Join(", ", updated.Errors.Select(error => error.Code)));
        }
    }

    public async Task SetPasswordAsync(Guid userId, string newPassword, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString()) ?? throw new UserNotFoundException(userId);

        if (user.TenantId != context.CurrentTenantId) throw new UserNotFoundException(userId);

        if (string.IsNullOrWhiteSpace(newPassword)) throw new ValidationFailedException("La contraseña no puede quedar vacía.");

        var policy = await passwordValidator.ValidateAsync(userManager, user, newPassword);

        if (!policy.Succeeded)
        {
            throw new ValidationFailedException(
                "La nueva contraseña no cumple las reglas de la política: "
                + string.Join(", ", policy.Errors.Select(error => error.Description)));
        }

        user.PasswordHash = userManager.PasswordHasher.HashPassword(user, newPassword);

        var updated = await userManager.UpdateAsync(user);

        if (!updated.Succeeded)
        {
            throw new ValidationFailedException("No se pudo guardar la contraseña: " + string.Join(", ", updated.Errors.Select(error => error.Code)));
        }
    }

    public async Task<string> GetRoleAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString()) ?? throw new UserNotFoundException(userId);

        if (user.TenantId != context.CurrentTenantId)
        {
            throw new UserNotFoundException(userId);
        }

        var roles = await userManager.GetRolesAsync(user);

        return roles.FirstOrDefault() ?? TenantRoles.Member;
    }

    public async Task<int> CountOtherTenantAdminsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var admins = await userManager.GetUsersInRoleAsync(TenantRoles.TenantAdmin);
        return admins.Count(admin => admin.TenantId == context.CurrentTenantId && admin.Id != userId);
    }

    public async Task SetRoleAsync(Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new UserNotFoundException(userId);

        if (user.TenantId != context.CurrentTenantId)
        {
            throw new UserNotFoundException(userId);
        }

        var current = await userManager.GetRolesAsync(user);

        if (current.Contains(role))
        {
            return;
        }

        foreach (var oldRole in current)
        {
            var removed = await userManager.RemoveFromRoleAsync(user, oldRole);

            if (!removed.Succeeded)
            {
                throw new ValidationFailedException("No se pudo quitar el rol anterior: " + string.Join(", ", removed.Errors.Select(error => error.Code)));
            }
        }

        var assigned = await userManager.AddToRoleAsync(user, role);

        if (!assigned.Succeeded)
        {
            throw new ValidationFailedException("No se pudo asignar el rol: " + string.Join(", ", assigned.Errors.Select(error => error.Code)));
        }
    }

    public async Task<string?> GetUserPhoneNumberAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString()) ?? throw new UserNotFoundException(userId);

        if (user.TenantId != context.CurrentTenantId) throw new UserNotFoundException(userId);

        return user.PhoneNumber;
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

    private bool IsLockedOut(bool lockoutEnabled, DateTimeOffset? lockoutEnd) =>
        lockoutEnabled
        && lockoutEnd is { } until
        && until > timeProvider.GetUtcNow();

    private async Task<LoginIdentity> ToIdentityAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var roles = await context.UserRoles
            .Where(userRole => userRole.UserId == userId)
            .Join(context.Roles, userRole => userRole.RoleId, role => role.Id, (_, role) => role.Name!)
            .ToListAsync(cancellationToken);

        return new LoginIdentity(userId, tenantId, roles);
    }
}

using Microsoft.EntityFrameworkCore;
using Npgsql;
using Stockma.Application.Identity;
using Stockma.Domain.Entities;
using Stockma.Infrastructure.Persistence;

namespace Stockma.Infrastructure.Identity;

public sealed class RefreshTokens(StockmaDbContext context) : IRefreshTokens
{
    private const string FindByHashSql =
        """
        SELECT id, tenant_id, user_id, family_id, device_id, expires_at, family_expires_at, consumed_at, revoked_at
          FROM auth_find_refresh_token_by_hash(@tokenHash);
        """;

    public async Task<RefreshTokenLookup?> FindByHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            return null;
        }

        var connection = context.Database.GetDbConnection();
        var wasClosed = connection.State != System.Data.ConnectionState.Open;

        if (wasClosed)
        {
            await context.Database.OpenConnectionAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = FindByHashSql;
            command.Parameters.Add(new NpgsqlParameter("tokenHash", tokenHash));

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            return new RefreshTokenLookup(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetGuid(2),
                reader.GetGuid(3),
                reader.GetString(4),
                reader.GetFieldValue<DateTimeOffset>(5),
                reader.GetFieldValue<DateTimeOffset>(6),
                reader.IsDBNull(7) ? null : reader.GetFieldValue<DateTimeOffset>(7),
                reader.IsDBNull(8) ? null : reader.GetFieldValue<DateTimeOffset>(8));
        }
        finally
        {
            if (wasClosed)
            {
                await context.Database.CloseConnectionAsync();
            }
        }
    }

    public async Task AddAsync(RefreshToken token, CancellationToken cancellationToken = default)
    {
        await context.RefreshTokens.AddAsync(token, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);

    public async Task RevokeFamilyAsync(
        Guid familyId,
        DateTimeOffset revokedAt,
        CancellationToken cancellationToken = default)
    {
        var live = await context.RefreshTokens
            .Where(token => token.FamilyId == familyId && token.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var token in live)
        {
            token.Revoke(revokedAt);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeByDeviceAsync(
        Guid userId,
        IReadOnlyCollection<string> deviceIds,
        DateTimeOffset revokedAt,
        CancellationToken cancellationToken = default)
    {
        if (deviceIds.Count == 0)
        {
            return;
        }

        var familyIds = await context.RefreshTokens
            .Where(token => token.UserId == userId
                && deviceIds.Contains(token.DeviceId)
                && token.RevokedAt == null)
            .Select(token => token.FamilyId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (familyIds.Count == 0)
        {
            return;
        }

        var live = await context.RefreshTokens
            .Where(token => familyIds.Contains(token.FamilyId) && token.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var token in live)
        {
            token.Revoke(revokedAt);
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}

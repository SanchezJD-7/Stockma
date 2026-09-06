using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Stockma.Application.Products;
using Stockma.Infrastructure.Persistence;

namespace Stockma.Infrastructure.Products;

public sealed class SkuGenerator(StockmaDbContext context) : ISkuGenerator
{
    public const string Prefix = "SKU-";
    private const string ConsumeNextNumberSql =
        """
        UPDATE tenant_settings
           SET next_sku_number = next_sku_number + 1
         WHERE tenant_id = @tenant
        RETURNING next_sku_number - 1;
        """;

    public async Task<string> NextAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = context.CurrentTenantId;
        var connection = context.Database.GetDbConnection();
        var wasClosed = connection.State != System.Data.ConnectionState.Open;

        if (wasClosed)
        {
            await context.Database.OpenConnectionAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = ConsumeNextNumberSql;
            command.Transaction = context.Database.CurrentTransaction?.GetDbTransaction();
            command.Parameters.Add(new NpgsqlParameter("tenant", tenantId));

            var result = await command.ExecuteScalarAsync(cancellationToken);
            if (result is null or DBNull)
            {
                throw new InvalidOperationException(
                    $"El tenant '{tenantId}' no tiene fila en tenant_settings: no se puede generar un SKU. " +
                    "El alta del tenant debe crear su configuración.");
            }

            return $"{Prefix}{Convert.ToInt32(result)}";
        }
        finally
        {
            if (wasClosed)
            {
                await context.Database.CloseConnectionAsync();
            }
        }
    }
}

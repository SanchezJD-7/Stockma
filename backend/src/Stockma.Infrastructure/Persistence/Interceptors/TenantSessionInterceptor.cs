using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Stockma.Application.Common;

namespace Stockma.Infrastructure.Persistence.Interceptors;

public sealed class TenantSessionInterceptor(ITenantContext tenantContext) : DbConnectionInterceptor
{
    private const string SetTenantSql = "SELECT set_config('app.tenant', @tenant, false);";
    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        if (tenantContext.IsResolved)
        {
            await using var command = CreateSetTenantCommand(connection, tenantContext.TenantId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
    }

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        if (tenantContext.IsResolved)
        {
            using var command = CreateSetTenantCommand(connection, tenantContext.TenantId);
            command.ExecuteNonQuery();
        }

        base.ConnectionOpened(connection, eventData);
    }

    private static DbCommand CreateSetTenantCommand(DbConnection connection, Guid tenantId)
    {
        var command = connection.CreateCommand();
        command.CommandText = SetTenantSql;
        var parameter = command.CreateParameter();
        parameter.ParameterName = "tenant";
        parameter.Value = tenantId.ToString();
        command.Parameters.Add(parameter);

        return command;
    }
}

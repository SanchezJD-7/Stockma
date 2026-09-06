using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Stockma.Application.Common;

namespace Stockma.Infrastructure.Persistence.DesignTime;

public sealed class StockmaDbContextFactory : IDesignTimeDbContextFactory<StockmaDbContext>
{
    private const string DefaultConnectionString = "Host=localhost;Port=5432;Database=stockma;Username=stockma;Password=stockma";
    public StockmaDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("STOCKMA_CONNECTION_STRING") ?? DefaultConnectionString;
        var options = new DbContextOptionsBuilder<StockmaDbContext>().UseNpgsql(connectionString).Options;
        return new StockmaDbContext(options, new DesignTimeTenantContext());
    }

    private sealed class DesignTimeTenantContext : ITenantContext
    {
        public Guid TenantId { get; } = Guid.Parse("00000000-0000-0000-0000-0000000000ff");
        public bool IsResolved => true;
        public void Set(Guid tenantId) => throw new NotSupportedException("El contexto de diseño no resuelve tenants.");
    }
}

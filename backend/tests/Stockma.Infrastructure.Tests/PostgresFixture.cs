using Microsoft.EntityFrameworkCore;
using Npgsql;
using Stockma.Application.Common;
using Stockma.Infrastructure.Persistence;
using Stockma.Infrastructure.Persistence.Interceptors;
using Stockma.Infrastructure.Tenancy;
using Testcontainers.PostgreSql;

namespace Stockma.Infrastructure.Tests;

public sealed class PostgresFixture : IAsyncLifetime
{
    public const string AppUserPassword = "app_user_test_pwd";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("stockma_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public string AppUserConnectionString =>
        new NpgsqlConnectionStringBuilder(ConnectionString)
        {
            Username = "app_user",
            Password = AppUserPassword,
        }.ConnectionString;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var tenantContext = new TenantContext();
        tenantContext.Set(Guid.NewGuid());

        var options = new DbContextOptionsBuilder<StockmaDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        await using var context = new StockmaDbContext(options, tenantContext);
        await context.Database.MigrateAsync();
        await context.Database.ExecuteSqlRawAsync($"ALTER ROLE app_user PASSWORD '{AppUserPassword}';");
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public StockmaDbContext CreateAppUserContext(ITenantContext tenantContext)
    {
        var options = new DbContextOptionsBuilder<StockmaDbContext>()
            .UseNpgsql(AppUserConnectionString)
            .AddInterceptors(new TenantSessionInterceptor(tenantContext))
            .Options;

        return new StockmaDbContext(options, tenantContext);
    }
}

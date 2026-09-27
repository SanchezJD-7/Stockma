using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Stockma.Api.Middleware;
using Stockma.Application.Identity;
using Npgsql;
using Stockma.Infrastructure.Persistence;
using Stockma.Infrastructure.Tenancy;
using Testcontainers.PostgreSql;

namespace Stockma.Api.Tests;

public sealed class StockmaApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string AppUserPassword = "app_user_api_test_pwd";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("stockma_api_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public string SuperuserConnectionString => _container.GetConnectionString();

    public string AppUserConnectionString =>
        new NpgsqlConnectionStringBuilder(SuperuserConnectionString)
        {
            Username = "app_user",
            Password = AppUserPassword,
        }.ConnectionString;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);
        builder.UseSetting("RateLimiting:Auth:PermitPerWindow", "1000");

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = AppUserConnectionString,
                ["Jwt:Key"] = "clave-de-firma-para-tests-de-al-menos-32-bytes-de-largo",
                ["Jwt:Issuer"] = "stockma-api",
                ["Jwt:Audience"] = "stockma-web",
                ["Jwt:ExpiresMinutes"] = "60",
            });
        });
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var tenantContext = new TenantContext();
        tenantContext.Set(Guid.NewGuid());

        await using var owner = new StockmaDbContext(
            new DbContextOptionsBuilder<StockmaDbContext>().UseNpgsql(SuperuserConnectionString).Options,
            tenantContext);

        await owner.Database.MigrateAsync();
        await owner.Database.ExecuteSqlRawAsync($"ALTER ROLE app_user PASSWORD '{AppUserPassword}';");
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _container.DisposeAsync();
    }

    public HttpClient CreateAuthenticatedClient(
        Guid headerTenantId,
        Guid tokenTenantId,
        string role = TenantRoles.Member,
        Guid? userId = null)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(TenantMiddleware.HeaderName, headerTenantId.ToString());

        using var scope = Services.CreateScope();
        var tokens = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        var token = tokens.Create(userId ?? Guid.NewGuid(), tokenTenantId, [role]);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Value);

        return client;
    }

    public HttpClient CreateAuthenticatedClient(Guid tenantId, string role = TenantRoles.Member) =>
        CreateAuthenticatedClient(tenantId, tenantId, role);
}

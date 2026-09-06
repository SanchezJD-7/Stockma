using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Stockma.Application.Common;
using Stockma.Application.Batches;
using Stockma.Application.Products;
using Stockma.Infrastructure.Persistence;
using Stockma.Infrastructure.Persistence.Interceptors;
using Stockma.Infrastructure.Batches;
using Stockma.Infrastructure.Products;
using Stockma.Infrastructure.Tenancy;

namespace Stockma.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddScoped<TenantSessionInterceptor>();
        services.AddDbContext<StockmaDbContext>((provider, options) =>
        {
            options
                .UseNpgsql(configuration.GetConnectionString("Postgres"))
                .AddInterceptors(provider.GetRequiredService<TenantSessionInterceptor>());
        });

        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IBatchRepository, BatchRepository>();
        services.AddScoped<ITenantSettingsProvider, TenantSettingsProvider>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<ISkuGenerator, SkuGenerator>();
        return services;
    }
}

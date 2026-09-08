using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Stockma.Application.Common;
using Stockma.Application.Batches;
using Stockma.Application.Products;
using Stockma.Application.Tenants;
using Stockma.Infrastructure.Persistence;
using Stockma.Infrastructure.Persistence.Interceptors;
using Stockma.Infrastructure.Batches;
using Stockma.Infrastructure.Tenants;
using Stockma.Infrastructure.Products;
using Stockma.Infrastructure.Tenancy;
using Stockma.Application.Identity;
using Stockma.Infrastructure.Identity;

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

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 12;
                options.Lockout.AllowedForNewUsers = true;
            })
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<StockmaDbContext>();

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.AddScoped<IJwtTokenService, JwtTokenService>();

        return services;
    }
}

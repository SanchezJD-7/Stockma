using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
        services.AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Postgres), DatabaseOptions.MissingRuntimeConnectionMessage)
            .ValidateOnStart();

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

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<JwtOptions>, JwtOptionsValidator>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();

        services.Configure<OtpOptions>(configuration.GetSection(OtpOptions.SectionName));
        services.AddSingleton<IOtpGenerator, OtpGenerator>();
        services.AddScoped<IDeviceOtpService, DeviceOtpService>();
        services.AddScoped<IUserAccounts, UserAccounts>();
        services.AddScoped<ITrustedDevices, TrustedDevices>();
        services.AddScoped<ITenantAccounts, TenantAccounts>();
        services.AddScoped<IRefreshTokens, RefreshTokens>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();

        return services;
    }

    public static IServiceCollection AddSmsSender(
        this IServiceCollection services,
        IConfiguration configuration,
        bool isDevelopment)
    {
        services
            .AddOptions<SmsOptions>()
            .Bind(configuration.GetSection(SmsOptions.SectionName))
            .Validate(
                options => isDevelopment || !string.IsNullOrWhiteSpace(options.Provider),
                SmsOptions.MissingProviderMessage)
            .Validate(options => isDevelopment || !options.IsConsole, SmsOptions.ConsoleOutsideDevelopmentMessage)
            .Validate(
                options => string.IsNullOrWhiteSpace(options.Provider) || options.IsConsole || options.IsTwilio,
                SmsOptions.UnknownProviderMessage)
            .Validate(
                options => !options.IsTwilio || !string.IsNullOrWhiteSpace(options.AccountSid),
                SmsOptions.MissingAccountSidMessage)
            .Validate(
                options => !options.IsTwilio || !string.IsNullOrWhiteSpace(options.ApiKey),
                SmsOptions.MissingApiKeyMessage)
            .Validate(options => !options.IsTwilio || options.HasWellFormedSender, SmsOptions.InvalidSenderMessage)
            .Validate(
                options => options.RetryCount is >= 0 and <= SmsOptions.MaximumRetryCount,
                SmsOptions.InvalidRetryCountMessage)
            .Validate(
                options => options.RetryDelayMs is >= 0 and <= SmsOptions.MaximumRetryDelayMs,
                SmsOptions.InvalidRetryDelayMessage)
            .ValidateOnStart();

        services.AddHttpClient<TwilioSmsSender>();

        services.AddScoped<ISmsSender>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<SmsOptions>>().Value;

            if (isDevelopment && (options.IsConsole || string.IsNullOrWhiteSpace(options.Provider)))
            {
                return new ConsoleSmsSender(provider.GetRequiredService<ILogger<ConsoleSmsSender>>());
            }

            if (options.IsTwilio)
            {
                return provider.GetRequiredService<TwilioSmsSender>();
            }

            throw new InvalidOperationException(
                options.IsConsole
                    ? SmsOptions.ConsoleOutsideDevelopmentMessage
                    : string.IsNullOrWhiteSpace(options.Provider)
                        ? SmsOptions.MissingProviderMessage
                        : SmsOptions.UnknownProviderMessage);
        });

        return services;
    }
}

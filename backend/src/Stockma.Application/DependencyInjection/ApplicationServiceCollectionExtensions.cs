using Microsoft.Extensions.DependencyInjection;

namespace Stockma.Application.DependencyInjection;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(configuration =>
            configuration.RegisterServicesFromAssembly(
                typeof(ApplicationServiceCollectionExtensions).Assembly));

        return services;
    }
}

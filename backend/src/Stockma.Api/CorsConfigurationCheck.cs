using Microsoft.Extensions.Configuration;

namespace Stockma.Api;

public sealed class CorsConfigurationCheck(
    IConfiguration configuration,
    ILogger<CorsConfigurationCheck> logger) : IHostedService
{
    public const string SectionName = "Cors:AllowedOrigins";
    public static bool AppliesTo(IWebHostEnvironment environment) => !environment.IsDevelopment();

    public static string[] AllowedOrigins(IConfiguration configuration) =>
        (configuration.GetSection(SectionName).Get<string[]>() ?? [])
            .Where(origin => !string.IsNullOrWhiteSpace(origin))
            .ToArray();

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var origins = AllowedOrigins(configuration);
        if (origins.Length == 0)
        {
            throw new InvalidOperationException(
                "Falta Cors:AllowedOrigins: definí la variable de entorno "
                + "Cors__AllowedOrigins__0 con el origen del frontend "
                + "(p. ej. https://tu-proyecto.vercel.app).");
        }

        logger.LogInformation("CORS habilitado para: {Origins}", string.Join(", ", origins));
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

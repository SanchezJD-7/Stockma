using Stockma.Infrastructure.Persistence;

namespace Stockma.Api;

public sealed class RuntimeDatabaseRoleCheck(IConfiguration configuration) : IHostedLifecycleService
{
    public static bool AppliesTo(IHostEnvironment environment) => !environment.IsDevelopment();

    public Task StartingAsync(CancellationToken cancellationToken) =>
        RuntimeDatabaseRole.EnsureRestrictedAsync(configuration.GetConnectionString("Postgres"), cancellationToken);

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using Stockma.Application.Identity.Commands;
using Stockma.Domain.Exceptions;
using Stockma.Infrastructure.Persistence;

namespace Stockma.Api.Cli;

public static class BootstrapDemoCommandLine
{
    public const string CommandName = "bootstrap-demo";
    public const string PasswordEnvironmentVariable = "STOCKMA_DEMO_PASSWORD";

    public static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public const string DefaultEmail = "demo@stockma.app";
    public const string DefaultPhoneNumber = "+10000000000";
    public const string DefaultPassword = "Demo#Stockma2026";

    public static async Task<int> RunAsync(string[] args, IServiceProvider services, TextWriter output, TextWriter error)
    {
        var refusal = await CheckStartupAsync(services);

        if (refusal is not null)
        {
            error.WriteLine($"{CommandName} no corre: {refusal}");
            return 1;
        }

        var arguments = ParseArguments(args);
        var password = Environment.GetEnvironmentVariable(PasswordEnvironmentVariable);

        if (string.IsNullOrWhiteSpace(password))
        {
            password = DefaultPassword;
        }

        using var scope = services.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        try
        {
            var result = await sender.Send(
                new BootstrapDemoCommand(arguments.TenantId, arguments.Email, password, arguments.PhoneNumber));

            output.WriteLine(
                $"Ambiente demo listo: tenant={arguments.TenantId} userId={result.UserId} email={result.Email} "
                + $"password={(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(PasswordEnvironmentVariable)) ? DefaultPassword : "desde " + PasswordEnvironmentVariable)}");
            return 0;
        }
        catch (DomainException exception)
        {
            error.WriteLine($"{exception.ErrorCode}: {exception.Message}");
            return 1;
        }
        catch (ArgumentException exception)
        {
            error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static async Task<string?> CheckStartupAsync(IServiceProvider services)
    {
        try
        {
            services.GetService<IStartupValidator>()?.Validate();

            if (RuntimeDatabaseRoleCheck.AppliesTo(services.GetRequiredService<IHostEnvironment>()))
            {
                await RuntimeDatabaseRole.EnsureRestrictedAsync(
                    services.GetRequiredService<IConfiguration>().GetConnectionString("Postgres"));
            }

            return null;
        }
        catch (Exception exception) when (exception is OptionsValidationException
                                              or AggregateException
                                              or FormatException
                                              or InvalidOperationException
                                              or NpgsqlException)
        {
            return exception.Message;
        }
    }

    private static Arguments ParseArguments(string[] args)
    {
        var result = new Arguments(DefaultTenantId, DefaultEmail, DefaultPhoneNumber);

        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--tenant" when i + 1 < args.Length && Guid.TryParse(args[i + 1], out var tenantId):
                    result = result with { TenantId = tenantId };
                    i++;
                    break;
                case "--email" when i + 1 < args.Length:
                    result = result with { Email = args[i + 1] };
                    i++;
                    break;
                case "--phone" when i + 1 < args.Length:
                    result = result with { PhoneNumber = args[i + 1] };
                    i++;
                    break;
            }
        }

        return result;
    }

    private sealed record Arguments(Guid TenantId, string Email, string PhoneNumber);
}

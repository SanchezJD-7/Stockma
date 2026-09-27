using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using Stockma.Application.Identity.Commands;
using Stockma.Domain.Exceptions;
using Stockma.Infrastructure.Persistence;

namespace Stockma.Api.Cli;

public static class BootstrapAdminCommandLine
{
    public const string CommandName = "bootstrap-admin";
    public const string PasswordEnvironmentVariable = "STOCKMA_BOOTSTRAP_PASSWORD";

    public static async Task<int> RunAsync(string[] args, IServiceProvider services, TextWriter output, TextWriter error)
    {
        var refusal = await CheckStartupAsync(services);

        if (refusal is not null)
        {
            error.WriteLine($"{CommandName} no corre: {refusal}");
            return 1;
        }

        var arguments = ParseArguments(args);

        if (arguments is null)
        {
            error.WriteLine(
                $"Uso: {CommandName} --tenant <guid> --email <email> --phone <telefono>. "
                + $"La contraseña se lee de la variable de entorno {PasswordEnvironmentVariable}.");
            return 1;
        }

        var password = Environment.GetEnvironmentVariable(PasswordEnvironmentVariable);

        if (string.IsNullOrWhiteSpace(password))
        {
            error.WriteLine(
                $"La variable de entorno {PasswordEnvironmentVariable} es obligatoria y no puede estar vacía.");
            return 1;
        }

        using var scope = services.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        try
        {
            var result = await sender.Send(new BootstrapAdminCommand(
                arguments.Value.TenantId,
                arguments.Value.Email,
                password,
                arguments.Value.Phone));

            output.WriteLine($"TenantAdmin creado: userId={result.UserId} email={result.Email}");
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

    private static (Guid TenantId, string Email, string Phone)? ParseArguments(string[] args)
    {
        Guid? tenantId = null;
        string? email = null;
        string? phone = null;

        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--tenant" when i + 1 < args.Length && Guid.TryParse(args[i + 1], out var parsedTenantId):
                    tenantId = parsedTenantId;
                    i++;
                    break;
                case "--email" when i + 1 < args.Length:
                    email = args[i + 1];
                    i++;
                    break;
                case "--phone" when i + 1 < args.Length:
                    phone = args[i + 1];
                    i++;
                    break;
            }
        }

        if (tenantId is null || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(phone))
        {
            return null;
        }

        return (tenantId.Value, email, phone);
    }
}

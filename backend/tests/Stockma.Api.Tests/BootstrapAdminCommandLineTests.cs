using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Stockma.Api.Cli;
using Stockma.Application.Identity;
using Stockma.Infrastructure.DependencyInjection;

namespace Stockma.Api.Tests;

public class BootstrapAdminCommandLineTests(StockmaApiFactory factory) : IClassFixture<StockmaApiFactory>
{
    private const string ValidJwtKey = "clave-de-firma-para-tests-de-al-menos-32-bytes-de-largo";

    private static readonly string[] Arguments =
    [
        BootstrapAdminCommandLine.CommandName,
        "--tenant", Guid.NewGuid().ToString(),
        "--email", "admin@droga.co",
        "--phone", "+573001234567",
    ];

    private sealed class RecordingSender : ISender
    {
        public int Sent { get; private set; }

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            Sent++;
            return Task.FromResult((TResponse)(object)new RegisteredUser(Guid.NewGuid(), "admin@droga.co"));
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest => throw new NotSupportedException();

        public Task<object?> Send(object request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private static IServiceProvider BuildServices(
        string environment,
        string connectionString,
        RecordingSender sender,
        string jwtKey = ValidJwtKey)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = connectionString,
                ["Jwt:Issuer"] = "stockma-api",
                ["Jwt:Audience"] = "stockma-web",
                ["Jwt:Key"] = jwtKey,
                ["Jwt:ExpiresMinutes"] = "15",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IHostEnvironment>(new HostingEnvironment { EnvironmentName = environment });
        services.AddInfrastructure(configuration);
        services.AddSingleton<ISender>(sender);

        return services.BuildServiceProvider();
    }

    private static async Task<(int ExitCode, string Error)> RunAsync(IServiceProvider services)
    {
        Environment.SetEnvironmentVariable(BootstrapAdminCommandLine.PasswordEnvironmentVariable, "S3gura#2026Larga");

        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await BootstrapAdminCommandLine.RunAsync(Arguments, services, output, error);

        return (exitCode, error.ToString());
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task OutsideDevelopment_AsASuperuser_ExitsNonZeroBeforeSendingTheCommand(string environment)
    {
        var sender = new RecordingSender();

        var (exitCode, error) = await RunAsync(BuildServices(environment, factory.SuperuserConnectionString, sender));

        exitCode.Should().NotBe(0);
        error.Should().Contain("superusuario");
        sender.Sent.Should().Be(0, "ADR-017: el CLI corre el mismo chequeo de rol que la API antes de tocar nada");
    }

    [Fact]
    public async Task WithAnInvalidJwtKey_ExitsNonZeroBeforeSendingTheCommand()
    {
        var sender = new RecordingSender();

        var (exitCode, error) = await RunAsync(
            BuildServices("Development", factory.AppUserConnectionString, sender, jwtKey: "corta"));

        exitCode.Should().NotBe(0);
        error.Should().Contain("Jwt:Key");
        sender.Sent.Should().Be(0, "ADR-017: el CLI valida las opciones igual que el arranque de la API");
    }

    [Fact]
    public async Task WithoutARuntimeConnectionString_ExitsNonZeroBeforeSendingTheCommand()
    {
        var sender = new RecordingSender();

        var (exitCode, error) = await RunAsync(BuildServices("Production", string.Empty, sender));

        exitCode.Should().NotBe(0);
        error.Should().Contain("ConnectionStrings:Postgres");
        sender.Sent.Should().Be(0);
    }

    [Fact]
    public async Task OutsideDevelopment_AsTheRestrictedAppUser_SendsTheCommand()
    {
        var sender = new RecordingSender();

        var (exitCode, _) = await RunAsync(BuildServices("Production", factory.AppUserConnectionString, sender));

        exitCode.Should().Be(0);
        sender.Sent.Should().Be(1);
    }

    [Fact]
    public async Task InDevelopment_TheRuntimeRoleIsNotChecked()
    {
        var sender = new RecordingSender();

        var (exitCode, _) = await RunAsync(BuildServices("Development", factory.SuperuserConnectionString, sender));

        exitCode.Should().Be(0);
        sender.Sent.Should().Be(1);
    }
}

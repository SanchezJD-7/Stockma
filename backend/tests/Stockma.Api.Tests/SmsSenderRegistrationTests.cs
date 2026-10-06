using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Stockma.Application.Identity;
using Stockma.Infrastructure.DependencyInjection;
using Stockma.Infrastructure.Identity;

namespace Stockma.Api.Tests;

public class SmsSenderRegistrationTests(StockmaApiFactory factory) : IClassFixture<StockmaApiFactory>
{
    [Fact]
    public void Development_UsesTheConsoleSender()
    {
        using var scope = factory.Services.CreateScope();

        scope.ServiceProvider.GetRequiredService<ISmsSender>().Should().BeOfType<ConsoleSmsSender>();
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void OutsideDevelopment_WithoutARealProvider_FailsAtStartup(string environment)
    {
        using var app = factory.WithWebHostBuilder(builder => builder.UseEnvironment(environment));

        var act = () => app.CreateClient();

        act.Should()
            .Throw<Exception>("ADR-016: fuera de Development nunca se cae en silencio al sender que loguea códigos")
            .Which.ToString().Should().Contain("proveedor de SMS");
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void OutsideDevelopment_WithTheConsoleProvider_FailsAtStartup(string environment)
    {
        using var app = factory.WithWebHostBuilder(builder => builder
            .UseEnvironment(environment)
            .ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["Sms:Provider"] = "console" })));

        var act = () => app.CreateClient();

        act.Should()
            .Throw<Exception>("ADR-016: el sender de consola escribe el código en el log, no manda SMS")
            .Which.ToString().Should().Contain("Sms:Provider 'console' sólo corre en Development");
    }

    [Theory]
    [InlineData("", "un-secreto", "+5491155551234", "Sms:AccountSid")]
    [InlineData("AC00000000000000000000000000000000", "", "+5491155551234", "Sms:ApiKey")]
    [InlineData("AC00000000000000000000000000000000", "un-secreto", "", "Sms:Sender")]
    public void OutsideDevelopment_WithTwilioAndMissingCredentials_FailsAtStartup(
        string accountSid,
        string apiKey,
        string sender,
        string missingSetting)
    {
        using var app = factory.WithWebHostBuilder(builder => builder
            .UseEnvironment("Production")
            .ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Sms:Provider"] = "twilio",
                    ["Sms:AccountSid"] = accountSid,
                    ["Sms:ApiKey"] = apiKey,
                    ["Sms:Sender"] = sender,
                })));

        var act = () => app.CreateClient();

        act.Should()
            .Throw<Exception>("T049: sin credenciales completas la API no puede mandar SMS reales")
            .Which.ToString().Should().Contain(missingSetting);
    }

    [Fact]
    public void Development_WithTwilioConfigured_UsesTheTwilioSender()
    {
        using var app = factory.WithWebHostBuilder(builder => builder
            .UseEnvironment("Development")
            .ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Sms:Provider"] = "twilio",
                    ["Sms:AccountSid"] = "AC00000000000000000000000000000000",
                    ["Sms:ApiKey"] = "un-secreto",
                    ["Sms:Sender"] = "+5491155551234",
                })));

        using var scope = app.Services.CreateScope();

        scope.ServiceProvider.GetRequiredService<ISmsSender>().Should().BeOfType<TwilioSmsSender>(
            "T049: en Development también se puede mandar SMS reales si el proveedor está configurado");
    }

    [Fact]
    public void OutsideDevelopment_WithoutAProvider_NeverFallsBackToTheConsoleSender()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSmsSender(new ConfigurationBuilder().Build(), isDevelopment: false);

        using var provider = services.BuildServiceProvider();

        var act = () => provider.GetRequiredService<ISmsSender>();

        var exception = act.Should()
            .Throw<OptionsValidationException>(
                "ADR-016: fuera de Development no hay sender de consola que rescatar: falla, no loguea el código");

        exception.Which.Failures.Should().Contain(failure => failure.Contains("proveedor de SMS"));
    }
}

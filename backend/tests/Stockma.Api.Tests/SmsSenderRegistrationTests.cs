using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Stockma.Application.Identity;
using Stockma.Infrastructure.DependencyInjection;
using Stockma.Infrastructure.Email;
using Stockma.Infrastructure.Identity;

namespace Stockma.Api.Tests;

public class SmsSenderRegistrationTests(StockmaApiFactory factory) : IClassFixture<StockmaApiFactory>
{
    private static IWebHostBuilder WithoutBrevoValidation(IWebHostBuilder builder) =>
        builder.ConfigureTestServices(services => services.RemoveAll<IValidateOptions<EmailOptions>>());

    [Fact]
    public void Development_UsesTheConsoleSender()
    {
        using var scope = factory.Services.CreateScope();

        scope.ServiceProvider.GetRequiredService<ISmsSender>().Should().BeOfType<ConsoleSmsSender>();
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void OutsideDevelopment_WithoutAProvider_StartsWithADisabledSender(string environment)
    {
        using var app = factory.WithWebHostBuilder(builder =>
            WithoutBrevoValidation(builder.UseEnvironment(environment)));

        using var client = app.CreateClient();

        using var scope = app.Services.CreateScope();

        scope.ServiceProvider.GetRequiredService<ISmsSender>().Should().BeOfType<DisabledSmsSender>(
            "el waitlist no necesita SMS: la API arranca sin proveedor y el 2FA se habilita después con Twilio");
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void OutsideDevelopment_WithTheConsoleProvider_FailsAtStartup(string environment)
    {
        using var app = factory.WithWebHostBuilder(builder => WithoutBrevoValidation(builder
            .UseEnvironment(environment)
            .ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["Sms:Provider"] = "console" }))));

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
        using var app = factory.WithWebHostBuilder(builder => WithoutBrevoValidation(builder
            .UseEnvironment("Production")
            .ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Sms:Provider"] = "twilio",
                    ["Sms:AccountSid"] = accountSid,
                    ["Sms:ApiKey"] = apiKey,
                    ["Sms:Sender"] = sender,
                }))));

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
    public async Task OutsideDevelopment_WithoutAProvider_NeverFallsBackToTheConsoleSender()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSmsSender(new ConfigurationBuilder().Build(), isDevelopment: false);

        using var provider = services.BuildServiceProvider();

        var sender = provider.GetRequiredService<ISmsSender>();

        sender.Should().BeOfType<DisabledSmsSender>(
            "ADR-016: fuera de Development no hay sender de consola que rescatar: el envío falla, no se loguea el código");

        var act = () => sender.SendAsync("+573043335365", "123456", CancellationToken.None);

        await act.Should().ThrowAsync<SmsDeliveryException>()
            .WithMessage("*Sms:Provider*",
                "sin proveedor el envío falla igual que si Twilio respondiera un error: el login responde igual y el motivo queda en el log");
    }
}

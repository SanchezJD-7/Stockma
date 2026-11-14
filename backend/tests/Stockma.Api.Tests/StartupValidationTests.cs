using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Stockma.Application.Waitlist;
using Stockma.Infrastructure.Email;
using Stockma.Infrastructure.Identity;

namespace Stockma.Api.Tests;

public class StartupValidationTests(StockmaApiFactory factory) : IClassFixture<StockmaApiFactory>
{
    private WebApplicationFactory<Program> With(
        string environment,
        params (string Key, string? Value)[] settings) =>
        factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(settings.ToDictionary(s => s.Key, s => s.Value)));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IValidateOptions<SmsOptions>>();
                services.RemoveAll<IValidateOptions<EmailOptions>>();
            });
        });

    private static void ShouldFailAtStartup(WebApplicationFactory<Program> app, string expected)
    {
        var act = () => app.Services;

        act.Should().Throw<Exception>("ADR-017: una configuración inválida corta el arranque, no la primera request")
            .Which.ToString().Should().Contain(expected);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("Testing")]
    public void OutsideDevelopment_AsASuperuser_RefusesToStart(string environment)
    {
        using var app = With(environment, ("ConnectionStrings:Postgres", factory.SuperuserConnectionString));

        ShouldFailAtStartup(app, "superusuario");
    }

    [Fact]
    public void OutsideDevelopment_AsTheRestrictedAppUser_Starts()
    {
        using var app = With("Production", ("ConnectionStrings:Postgres", factory.AppUserConnectionString));

        var act = () => app.CreateClient();

        act.Should().NotThrow();
    }

    [Fact]
    public void InDevelopment_TheRuntimeRoleIsNotChecked()
    {
        using var app = With("Development", ("ConnectionStrings:Postgres", factory.SuperuserConnectionString));

        var act = () => app.CreateClient();

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public void AMissingRuntimeConnectionString_FailsAtStartup(string environment)
    {
        using var app = With(environment, ("ConnectionStrings:Postgres", ""));

        ShouldFailAtStartup(app, "ConnectionStrings:Postgres");
    }

    [Fact]
    public void TheBaseAppSettings_CarryNoRuntimeConnectionString()
    {
        var baseSettings = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        baseSettings.GetConnectionString("Postgres")
            .Should()
            .BeNullOrEmpty("ADR-017: un deploy sin override tiene que fallar al arrancar, no probar una contraseña conocida");
    }

    [Theory]
    [InlineData("corta")]
    [InlineData("")]
    public void AJwtKeyShorterThan32Bytes_FailsAtStartup(string key)
    {
        using var app = With("Development", ("Jwt:Key", key));

        ShouldFailAtStartup(app, "Jwt:Key");
    }

    [Fact]
    public void AMissingJwtIssuer_FailsAtStartup()
    {
        using var app = With("Development", ("Jwt:Issuer", ""));

        ShouldFailAtStartup(app, "Jwt:Issuer");
    }

    [Theory]
    [InlineData("0")]
    [InlineData("16")]
    public void AJwtLifetimeOutsideOneToFifteenMinutes_FailsAtStartup(string minutes)
    {
        using var app = With("Development", ("Jwt:ExpiresMinutes", minutes));

        ShouldFailAtStartup(app, "Jwt:ExpiresMinutes");
    }

    [Fact]
    public void AnUnparseableKnownProxy_FailsAtStartup()
    {
        using var app = With("Development", ("ForwardedHeaders:KnownProxies:0", "no-es-una-ip"));

        ShouldFailAtStartup(app, "ForwardedHeaders:KnownProxies");
    }

    [Theory]
    [InlineData("10.0.0.0")]
    [InlineData("10.0.0.0/99")]
    [InlineData("no-es-una-red/8")]
    public void AnUnparseableKnownNetwork_FailsAtStartup(string network)
    {
        using var app = With("Development", ("ForwardedHeaders:KnownNetworks:0", network));

        ShouldFailAtStartup(app, "ForwardedHeaders:KnownNetworks");
    }

    [Fact]
    public void TheBaseAppSettings_CarryNoSendGridSecrets()
    {
        var baseSettings = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        var sendGrid = baseSettings.GetSection("SendGrid");
        sendGrid.Exists().Should().BeTrue("la sección SendGrid está cableada en la configuración base");
        sendGrid["ApiKey"].Should()
            .BeNullOrEmpty("las credenciales van en user-secrets o variables de entorno, nunca en el repositorio");
        sendGrid["TemplateId"].Should()
            .BeNullOrEmpty("el ID de la plantilla se configura por entorno, no se versiona");
        sendGrid["FromEmail"].Should().BeNullOrEmpty("el remitente se configura por entorno, no se versiona");
        sendGrid["FromName"].Should().BeNullOrEmpty("el nombre del remitente se configura por entorno, no se versiona");
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void OutsideDevelopment_AMissingSendGridApiKey_FailsAtStartup(string environment)
    {
        using var app = factory.WithWebHostBuilder(builder => builder
            .UseEnvironment(environment)
            .ConfigureTestServices(services => services.RemoveAll<IValidateOptions<SmsOptions>>()));

        ShouldFailAtStartup(app, "SendGrid:ApiKey");
    }

    [Theory]
    [InlineData("SG.test-key", "", "hola@stockma.app", "SendGrid:TemplateId")]
    [InlineData("SG.test-key", "d-abc123", "", "SendGrid:FromEmail")]
    public void OutsideDevelopment_AMissingSendGridSetting_FailsAtStartup(
        string apiKey,
        string templateId,
        string fromEmail,
        string expectedSetting)
    {
        using var app = factory.WithWebHostBuilder(builder => builder
            .UseEnvironment("Production")
            .ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["SendGrid:ApiKey"] = apiKey,
                    ["SendGrid:TemplateId"] = templateId,
                    ["SendGrid:FromEmail"] = fromEmail,
                }))
            .ConfigureTestServices(services => services.RemoveAll<IValidateOptions<SmsOptions>>()));

        ShouldFailAtStartup(app, expectedSetting);
    }

    [Fact]
    public void InDevelopment_WithoutSendGridConfiguration_TheApiStarts()
    {
        using var app = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Development"));

        var act = () => app.CreateClient();

        act.Should().NotThrow("en Development las credenciales se cargan con user-secrets cuando haga falta");
    }

    [Fact]
    public void Development_WithSendGridConfigured_ResolvesTheSendGridEmailSender()
    {
        using var app = factory.WithWebHostBuilder(builder => builder
            .UseEnvironment("Development")
            .ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["SendGrid:ApiKey"] = "SG.test-key",
                    ["SendGrid:TemplateId"] = "d-abc123",
                    ["SendGrid:FromEmail"] = "hola@stockma.app",
                })));

        using var scope = app.Services.CreateScope();

        scope.ServiceProvider.GetRequiredService<IEmailSender>().Should()
            .BeOfType<EmailSender>("el registro sigue el patrón de AddSmsSender: cliente HttpClient + IEmailSender");
    }
}

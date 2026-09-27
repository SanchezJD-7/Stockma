using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
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
            builder.ConfigureTestServices(services => services.RemoveAll<IValidateOptions<SmsOptions>>());
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
    [InlineData("61")]
    public void AJwtLifetimeOutsideOneToSixtyMinutes_FailsAtStartup(string minutes)
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
}

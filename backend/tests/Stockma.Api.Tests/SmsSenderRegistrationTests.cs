using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
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

    [Fact]
    public void OutsideDevelopment_NeverRegistersTheConsoleSender()
    {
        var services = new ServiceCollection();

        services.AddSmsSender(isDevelopment: false);

        services.Should().NotContain(descriptor => descriptor.ImplementationType == typeof(ConsoleSmsSender));
    }
}

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Stockma.Infrastructure.Identity;

namespace Stockma.Api.Tests;

public class ForwardedHeadersTests(StockmaApiFactory factory) : IClassFixture<StockmaApiFactory>
{
    private const string ProxyAddress = "10.0.0.5";

    private sealed class RemoteAddressStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, nextMiddleware) =>
            {
                context.Connection.RemoteIpAddress = IPAddress.Parse(ProxyAddress);
                await nextMiddleware(context);
            });

            next(app);
        };
    }

    private WebApplicationFactory<Program> BehindProxy(params (string Key, string Value)[] settings) =>
        factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("RateLimiting:Auth:PermitPerWindow", "2");

            foreach (var (key, value) in settings)
            {
                builder.UseSetting(key, value);
            }

            builder.ConfigureServices(services => services.AddSingleton<IStartupFilter, RemoteAddressStartupFilter>());
        });

    private static async Task<List<HttpStatusCode>> LoginFromThreeForwardedClientsAsync(HttpClient client)
    {
        var statuses = new List<HttpStatusCode>();

        foreach (var clientAddress in new[] { "203.0.113.1", "203.0.113.2", "203.0.113.3" })
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
            {
                Content = JsonContent.Create(new
                {
                    email = $"proxy-{Guid.NewGuid():N}@droga.co",
                    password = "Contrasena-Larga-1",
                    deviceId = "web-chrome-a91f2c77",
                }),
            };
            request.Headers.Add("X-Forwarded-For", clientAddress);

            statuses.Add((await client.SendAsync(request)).StatusCode);
        }

        return statuses;
    }

    [Fact]
    public async Task ForwardedFor_FromAnUnknownSource_IsIgnoredByTheRateLimiter()
    {
        using var app = BehindProxy();

        var statuses = await LoginFromThreeForwardedClientsAsync(app.CreateClient());

        statuses[2].Should().Be(
            HttpStatusCode.TooManyRequests,
            "ADR-016: sin proxies conocidos configurados, un X-Forwarded-For inventado no abre una cuota nueva");
    }

    [Fact]
    public async Task ForwardedFor_FromAKnownProxy_PartitionsTheRateLimitByTheRealClient()
    {
        using var app = BehindProxy(("ForwardedHeaders:KnownProxies:0", ProxyAddress));

        var statuses = await LoginFromThreeForwardedClientsAsync(app.CreateClient());

        statuses.Should().AllSatisfy(status => status.Should().Be(
            HttpStatusCode.Unauthorized,
            "detrás de un proxy conocido, cada cliente real tiene su propia cuota"));
    }

    private WebApplicationFactory<Program> InProductionBehindAKnownProxy() =>
        factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("https_port", "443");
            builder.UseSetting("ForwardedHeaders:KnownProxies:0", ProxyAddress);
            builder.ConfigureServices(services => services.AddSingleton<IStartupFilter, RemoteAddressStartupFilter>());
            builder.ConfigureTestServices(services => services.RemoveAll<IValidateOptions<SmsOptions>>());
        });

    private static async Task<HttpResponseMessage> GetProductsAsync(HttpClient client, string? forwardedProto)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/products?query=x");

        if (forwardedProto is not null)
        {
            request.Headers.Add("X-Forwarded-Proto", forwardedProto);
        }

        return await client.SendAsync(request);
    }

    [Fact]
    public async Task HttpsRedirection_OnPlainHttpWithoutForwardedProto_Redirects()
    {
        using var app = InProductionBehindAKnownProxy();
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await GetProductsAsync(client, forwardedProto: null);

        response.StatusCode.Should().Be(HttpStatusCode.TemporaryRedirect, "control: la redirección está activa fuera de Development");
    }

    [Fact]
    public async Task HttpsRedirection_BehindAKnownProxyForwardingHttps_DoesNotRedirect()
    {
        using var app = InProductionBehindAKnownProxy();
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await GetProductsAsync(client, forwardedProto: "https");

        response.StatusCode.Should().NotBe(
            HttpStatusCode.TemporaryRedirect,
            "ADR-017: UseForwardedHeaders corre antes que UseHttpsRedirection, así el esquema real del proxy evita el bucle de redirecciones");
        response.Headers.Location.Should().BeNull();
    }

    [Fact]
    public async Task ForwardedFor_FromAKnownNetwork_PartitionsTheRateLimitByTheRealClient()
    {
        using var app = BehindProxy(("ForwardedHeaders:KnownNetworks:0", "10.0.0.0/8"));

        var statuses = await LoginFromThreeForwardedClientsAsync(app.CreateClient());

        statuses.Should().AllSatisfy(status => status.Should().Be(HttpStatusCode.Unauthorized));
    }
}

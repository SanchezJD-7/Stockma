using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Stockma.Application.Identity;

namespace Stockma.Api.Tests;

public class LogoutEndpointsTests(StockmaApiFactory factory) : IClassFixture<StockmaApiFactory>
{
    private const string DeviceId = "web-chrome-a91f2c77";
    private const string Fingerprint = "fp-mostrador";
    private const string OtpCode = "483920";
    private const string Password = "Contrasena-Larga-1";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed class FixedOtpGenerator : IOtpGenerator
    {
        public string Generate() => OtpCode;
    }

    private WebApplicationFactory<Program> WithKnownOtp() =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.Replace(ServiceDescriptor.Singleton<IOtpGenerator, FixedOtpGenerator>())));

    private async Task<string> ArrangeSessionAsync(WebApplicationFactory<Program> app)
    {
        var tenantId = Guid.NewGuid();
        var email = $"logout-{Guid.NewGuid():N}@droga.co";

        using (var scope = factory.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<Stockma.Application.Common.ITenantContext>().Set(tenantId);
            var context = scope.ServiceProvider.GetRequiredService<Stockma.Infrastructure.Persistence.StockmaDbContext>();
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO tenants (id) VALUES ({tenantId}) ON CONFLICT DO NOTHING");
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO tenant_settings (tenant_id, max_trusted_devices, green_months, yellow_months, next_sku_number) VALUES ({tenantId}, 2, 6, 3, 1) ON CONFLICT DO NOTHING");
            await scope.ServiceProvider.GetRequiredService<IUserAccounts>().CreateAsync(
                new NewUser(tenantId, email, Password, "+5491100000055", TenantRoles.Member));
        }

        var client = app.CreateClient();

        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password, deviceId = DeviceId });
        login.StatusCode.Should().Be(HttpStatusCode.OK);

        var confirm = await client.PostAsJsonAsync(
            "/api/auth/confirm-device",
            new { email, deviceId = DeviceId, fingerprint = Fingerprint, otp = OtpCode });
        confirm.StatusCode.Should().Be(HttpStatusCode.OK);

        var cookie = confirm.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("stockma_refresh="));
        cookie.Should().NotBeNullOrEmpty();

        return cookie;
    }

    private static async Task<HttpResponseMessage> LogoutAsync(HttpClient client, string? cookie = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");

        if (cookie is not null)
        {
            request.Headers.Add("Cookie", cookie);
        }

        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> RefreshAsync(HttpClient client, string cookie)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh")
        {
            Content = JsonContent.Create(new { deviceId = DeviceId }),
        };
        request.Headers.Add("Cookie", cookie);
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task Logout_WithAValidCookie_Returns204AndClearsTheCookie()
    {
        using var app = WithKnownOtp();
        var cookie = await ArrangeSessionAsync(app);
        var client = app.CreateClient();

        var response = await LogoutAsync(client, cookie);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var cleared = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("stockma_refresh="));
        cleared.Should().Contain("Max-Age=0", "la cookie se borra del cliente");
    }

    [Fact]
    public async Task Logout_RevokesTheFamilyOnTheServerSide()
    {
        using var app = WithKnownOtp();
        var cookie = await ArrangeSessionAsync(app);
        var client = app.CreateClient();

        await LogoutAsync(client, cookie);

        var refresh = await RefreshAsync(client, cookie);

        refresh.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "después de logout la familia queda revocada");
    }

    [Fact]
    public async Task Logout_WithoutACookie_Returns204()
    {
        using var app = WithKnownOtp();
        var client = app.CreateClient();

        var response = await LogoutAsync(client);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent, "logout es idempotente y no revela si había cookie");
    }

    [Fact]
    public async Task Logout_WithAnUnknownToken_Returns204()
    {
        using var app = WithKnownOtp();
        var client = app.CreateClient();

        var response = await LogoutAsync(client, "stockma_refresh=token-que-no-existe");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent, "logout no revela si la cookie era válida");
    }
}

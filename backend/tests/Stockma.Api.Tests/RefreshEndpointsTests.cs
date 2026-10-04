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

public class RefreshEndpointsTests(StockmaApiFactory factory) : IClassFixture<StockmaApiFactory>
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

    private async Task<(string Email, string RefreshCookie)> ArrangeSessionAsync(WebApplicationFactory<Program> app)
    {
        var tenantId = Guid.NewGuid();
        var email = $"refresh-{Guid.NewGuid():N}@droga.co";

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

        return (email, cookie);
    }

    private static async Task<HttpResponseMessage> RefreshAsync(
        HttpClient client,
        string deviceId,
        string? cookie = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh")
        {
            Content = JsonContent.Create(new { deviceId }),
        };

        if (cookie is not null)
        {
            request.Headers.Add("Cookie", cookie);
        }

        return await client.SendAsync(request);
    }

    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).TryGetProperty("errorCode", out var code)
            ? code.GetString()
            : null;

    [Fact]
    public async Task Refresh_WithAValidCookie_RotatesAndReturnsNewTokens()
    {
        using var app = WithKnownOtp();
        var (_, cookie) = await ArrangeSessionAsync(app);
        var client = app.CreateClient();

        var response = await RefreshAsync(client, DeviceId, cookie);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        body.GetProperty("accessToken").GetString().Should().NotBeNullOrEmpty();
        body.GetProperty("expiresIn").GetInt32().Should().Be(900);

        var newCookie = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("stockma_refresh="));
        newCookie.Should().NotBe(cookie, "la rotación emite un token nuevo, no el mismo");
    }

    [Fact]
    public async Task Refresh_WithoutACookie_IsRejected()
    {
        using var app = WithKnownOtp();
        var client = app.CreateClient();

        var response = await RefreshAsync(client, DeviceId);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await ErrorCodeAsync(response)).Should().Be("AUTH_REFRESH_REJECTED");
    }

    [Fact]
    public async Task Refresh_WithAnUnknownToken_IsRejected()
    {
        using var app = WithKnownOtp();
        var client = app.CreateClient();

        var response = await RefreshAsync(client, DeviceId, "stockma_refresh=token-que-no-existe");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await ErrorCodeAsync(response)).Should().Be("AUTH_REFRESH_REJECTED");
    }

    [Fact]
    public async Task Refresh_WithAConsumedToken_RevokesTheWholeFamily()
    {
        using var app = WithKnownOtp();
        var (_, cookie) = await ArrangeSessionAsync(app);
        var client = app.CreateClient();

        var first = await RefreshAsync(client, DeviceId, cookie);
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        var reuse = await RefreshAsync(client, DeviceId, cookie);

        reuse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await ErrorCodeAsync(reuse)).Should().Be("AUTH_REFRESH_REJECTED");
    }

    [Fact]
    public async Task Refresh_WithADifferentDeviceId_RevokesTheFamily()
    {
        using var app = WithKnownOtp();
        var (_, cookie) = await ArrangeSessionAsync(app);
        var client = app.CreateClient();

        var response = await RefreshAsync(client, "otro-dispositivo-9999", cookie);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await ErrorCodeAsync(response)).Should().Be("AUTH_REFRESH_REJECTED");
    }

    [Fact]
    public async Task Refresh_IgnoresTheTenantHeader()
    {
        using var app = WithKnownOtp();
        var (_, cookie) = await ArrangeSessionAsync(app);
        var client = app.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-ID", Guid.NewGuid().ToString());

        var response = await RefreshAsync(client, DeviceId, cookie);

        response.StatusCode.Should().Be(HttpStatusCode.OK, "el header de tenant no decide a qué tenant se renueva");
    }

    [Fact]
    public async Task Refresh_WithoutADeviceId_Returns400()
    {
        using var app = WithKnownOtp();
        var client = app.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh")
        {
            Content = JsonContent.Create(new { }),
        };

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ErrorCodeAsync(response)).Should().Be("VALIDATION_FAILED");
    }

    [Fact]
    public async Task Refresh_TwoParallelRefreshesWithTheSameToken_ExactlyOneRotatesAndTheFamilyIsRevoked()
    {
        using var app = WithKnownOtp();
        var (_, cookie) = await ArrangeSessionAsync(app);
        var client = app.CreateClient();

        var first = RefreshAsync(client, DeviceId, cookie);
        var second = RefreshAsync(client, DeviceId, cookie);

        var responses = await Task.WhenAll(first, second);

        var statuses = responses.Select(response => response.StatusCode).ToArray();

        statuses.Count(status => status == HttpStatusCode.OK)
            .Should()
            .Be(1, "exactamente uno de los dos refresh en paralelo rota");

        statuses.Count(status => status == HttpStatusCode.Unauthorized)
            .Should()
            .Be(1, "el segundo ve el token consumido: es un reuso y revoca la familia");

        var afterRace = await RefreshAsync(client, DeviceId, cookie);

        afterRace.StatusCode
            .Should()
            .Be(HttpStatusCode.Unauthorized, "la familia quedó revocada: el último token emitido tampoco sirve");
    }
}

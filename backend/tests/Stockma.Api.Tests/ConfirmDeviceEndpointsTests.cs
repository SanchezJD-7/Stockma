using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Stockma.Application.Common;
using Stockma.Application.Identity;
using Stockma.Infrastructure.Persistence;

namespace Stockma.Api.Tests;

public class ConfirmDeviceEndpointsTests(StockmaApiFactory factory) : IClassFixture<StockmaApiFactory>
{
    private const string DeviceId = "web-chrome-a91f2c77";
    private const string Password = "Contrasena-Larga-1";
    private const string Code = "483920";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed class FixedOtpGenerator : IOtpGenerator
    {
        public string Generate() => Code;
    }

    private WebApplicationFactory<Program> WithKnownOtp() =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.Replace(ServiceDescriptor.Singleton<IOtpGenerator, FixedOtpGenerator>())));

    private async Task<string> SeedMemberAsync()
    {
        var tenantId = Guid.NewGuid();
        var email = $"confirm-{Guid.NewGuid():N}@droga.co";

        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(tenantId);
        var context = scope.ServiceProvider.GetRequiredService<StockmaDbContext>();

        await context.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO tenants (id) VALUES ({tenantId})");
        await scope.ServiceProvider.GetRequiredService<IUserAccounts>().CreateAsync(
            new NewUser(tenantId, email, Password, "+5491100000055", TenantRoles.Member));

        return email;
    }

    private static async Task LoginFromANewDeviceAsync(HttpClient client, string email)
    {
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password, deviceId = DeviceId });

        login.StatusCode.Should().Be(HttpStatusCode.OK);
        (await login.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .GetProperty("requiresDeviceConfirmation").GetBoolean().Should().BeTrue();
    }

    private static Task<HttpResponseMessage> ConfirmAsync(HttpClient client, string email, string fingerprint, string otp = Code) =>
        client.PostAsJsonAsync("/api/auth/confirm-device", new { email, deviceId = DeviceId, fingerprint, otp });

    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).TryGetProperty("errorCode", out var code)
            ? code.GetString()
            : null;

    private async Task LockOutAsync(string email)
    {
        await using var connection = new NpgsqlConnection(factory.SuperuserConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "UPDATE users SET lockout_end = now() + interval '1 day' WHERE normalized_email = upper(@email);",
            connection);
        command.Parameters.AddWithValue("email", email);
        (await command.ExecuteNonQueryAsync()).Should().Be(1);
    }

    [Fact]
    public async Task ConfirmDevice_WithAFingerprintLongerThan256_Returns400AndKeepsTheOtpUsable()
    {
        var email = await SeedMemberAsync();
        using var app = WithKnownOtp();
        var client = app.CreateClient();
        await LoginFromANewDeviceAsync(client, email);

        var tooLong = await ConfirmAsync(client, email, new string('f', 257));

        tooLong.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ErrorCodeAsync(tooLong)).Should().Be("VALIDATION_FAILED");

        var retry = await ConfirmAsync(client, email, "fp-mostrador");

        retry.StatusCode.Should().Be(HttpStatusCode.OK, "ADR-017: una falla después de validar no puede dejar el OTP gastado y sin JWT");
        (await retry.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("accessToken").GetString()
            .Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ConfirmDevice_ForALockedOutUser_IsRejectedLikeAnUnknownEmail()
    {
        var email = await SeedMemberAsync();
        using var app = WithKnownOtp();
        var client = app.CreateClient();
        await LoginFromANewDeviceAsync(client, email);
        await LockOutAsync(email);

        var locked = await ConfirmAsync(client, email, "fp-mostrador");
        var unknown = await ConfirmAsync(client, $"fantasma-{Guid.NewGuid():N}@droga.co", "fp-mostrador");

        locked.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "un usuario deshabilitado no obtiene JWT ni con el código correcto");
        (await locked.Content.ReadAsStringAsync()).Should().Be(
            await unknown.Content.ReadAsStringAsync(),
            "ADR-016: la respuesta no distingue bloqueado de inexistente");
    }

    [Fact]
    public async Task ConfirmDevice_ForALockedOutUser_DoesNotSpendTheOtp()
    {
        var email = await SeedMemberAsync();
        using var app = WithKnownOtp();
        var client = app.CreateClient();
        await LoginFromANewDeviceAsync(client, email);
        await LockOutAsync(email);

        await ConfirmAsync(client, email, "fp-mostrador");

        await using var connection = new NpgsqlConnection(factory.SuperuserConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT count(*) FROM device_otps o JOIN users u ON u.id = o.user_id
             WHERE u.normalized_email = upper(@email) AND o.consumed_at IS NULL AND o.failed_attempts = 0;
            """,
            connection);
        command.Parameters.AddWithValue("email", email);

        ((long)(await command.ExecuteScalarAsync())!).Should().Be(1, "al bloqueado se lo corta antes de tocar el OTP");
    }
}

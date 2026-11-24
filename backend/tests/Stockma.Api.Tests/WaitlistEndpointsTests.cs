using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Stockma.Infrastructure.Persistence;

namespace Stockma.Api.Tests;

public class WaitlistEndpointsTests(StockmaApiFactory factory) : IClassFixture<StockmaApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private static object NewEntry(
        string fullName = "Juan Pérez",
        string countryCode = "+57",
        string phone = "300 123 4567",
        string email = "Juan@Ejemplo.com") =>
        new { fullName, countryCode, phone, email };

    private static async Task<JsonElement> ReadPayloadAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

    private static async Task<string?> ReadErrorCodeAsync(HttpResponseMessage response)
    {
        var payload = await ReadPayloadAsync(response);

        return payload.TryGetProperty("errorCode", out var errorCode)
            ? errorCode.GetString()
            : null;
    }

    [Fact]
    public async Task Post_WithoutTenantHeaderOrJwt_Returns201WithNormalizedEmail()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/waitlist", NewEntry());

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var payload = await ReadPayloadAsync(response);
        payload.GetProperty("email").GetString().Should().Be("juan@ejemplo.com", "el 201 devuelve el correo normalizado");
        payload.GetProperty("id").GetGuid().Should().NotBeEmpty();
    }

    [Fact]
    public async Task Post_WithInvalidFields_Returns400WithFieldErrors()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/waitlist",
            NewEntry(fullName: "A", phone: "12", email: "not-an-email"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var payload = await ReadPayloadAsync(response);
        payload.GetProperty("errorCode").GetString().Should().Be("VALIDATION_FAILED");
        payload.TryGetProperty("errors", out var errors)
            .Should().BeTrue("el contrato exige mapa de campos en el 400");
        var errorKeys = string.Join(",", errors.EnumerateObject().Select(entry => entry.Name));
        errors.TryGetProperty("email", out _).Should().BeTrue($"claves presentes: {errorKeys}");
        errors.TryGetProperty("fullName", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Post_WithADuplicateEmail_Returns409()
    {
        await SeedWaitlistEntryAsync("ya@existe.com");

        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/waitlist", NewEntry(email: "YA@Existe.com"));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict, "el índice único de lower(email) es la fuente de verdad");
        (await ReadErrorCodeAsync(response)).Should().Be("WAITLIST_EMAIL_DUPLICATE");
    }

    [Fact]
    public async Task Post_WithADuplicateEmail_DoesNotLeakStackTrace()
    {
        await SeedWaitlistEntryAsync("stack@existe.com");

        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/waitlist", NewEntry(email: "stack@existe.com"));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var payload = await ReadPayloadAsync(response);
        payload.TryGetProperty("stackTrace", out _).Should().BeFalse("ningún error filtra stack trace");
        payload.TryGetProperty("trace", out _).Should().BeFalse();
    }

    private async Task SeedWaitlistEntryAsync(string email)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<StockmaDbContext>();

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO waitlist_entries (id, full_name, country_code, phone, email, created_at) VALUES ({Guid.NewGuid()}, 'Persona Existente', '+57', '3001234567', {email}, NOW()) ON CONFLICT DO NOTHING");
    }
}

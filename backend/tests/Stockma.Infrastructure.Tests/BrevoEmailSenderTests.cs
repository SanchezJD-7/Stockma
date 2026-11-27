using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Stockma.Application.Waitlist;
using Stockma.Domain.Exceptions;
using Stockma.Infrastructure.Email;

namespace Stockma.Infrastructure.Tests;

public class BrevoEmailSenderTests
{
    private const string ApiKey = "xkeysib-test-key";
    private const int TemplateId = 27;
    private const string FromEmail = "hola@stockma.app";
    private const string FromName = "Stockma";
    private const string RecipientEmail = "juan@ejemplo.com";
    private const string RecipientName = "Juan Pérez";
    private const string BrevoMailEndpoint = "https://api.brevo.com/v3/smtp/email";

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpResponseMessage>> results = new();
        private readonly List<(Uri Uri, string? Authorization, string? ApiKey, string Body)> requests = new();

        public IReadOnlyList<(Uri Uri, string? Authorization, string? ApiKey, string Body)> Requests => requests;

        public int CallCount => requests.Count;

        public void RespondWith(HttpStatusCode statusCode, string body = "") =>
            results.Enqueue(() => new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });

        public void ThrowWith(Exception exception) => results.Enqueue(() => throw exception);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            requests.Add((
                request.RequestUri!,
                request.Headers.Authorization?.Scheme,
                request.Headers.TryGetValues("api-key", out var values) ? values.Single() : null,
                body));

            if (results.Count == 0)
            {
                throw new InvalidOperationException("El test no preparó una respuesta para este llamado.");
            }

            return results.Dequeue()();
        }
    }

    private static EmailOptions NewOptions() => new()
    {
        ApiKey = ApiKey,
        FromEmail = FromEmail,
        FromName = FromName,
        TemplateId = TemplateId,
    };

    private static EmailSender CreateSender(StubHandler handler, EmailOptions? options = null) =>
        new(
            new HttpClient(handler),
            Options.Create(options ?? NewOptions()),
            NullLogger<EmailSender>.Instance);

    private static EmailMessage NewMessage() => new(RecipientEmail, RecipientName);

    [Fact]
    public async Task SendAsync_PostsTheTemplateIdAndTheNameAsParams()
    {
        var handler = new StubHandler();
        handler.RespondWith(HttpStatusCode.Accepted);
        var sender = CreateSender(handler);

        await sender.SendAsync(NewMessage());

        var request = handler.Requests.Should().ContainSingle().Subject;
        request.Uri.Should()
            .Be(new Uri(BrevoMailEndpoint), "el envío usa la API smtp/email de Brevo");
        request.Authorization.Should()
            .BeNull("Brevo no autentica con Authorization: Bearer");
        request.ApiKey.Should()
            .Be(ApiKey, "Brevo recibe la API key en el header api-key");

        using var payload = JsonDocument.Parse(request.Body);
        var root = payload.RootElement;

        root.GetProperty("templateId").GetInt32()
            .Should()
            .Be(TemplateId, "Brevo espera templateId como número, no como string con prefijo d-");

        var to = root.GetProperty("to").EnumerateArray().Should().ContainSingle().Subject;
        to.GetProperty("email").GetString().Should().Be(RecipientEmail);
        to.GetProperty("name").GetString().Should().Be(RecipientName, "la plantilla recibe el nombre del destinatario");

        var templateParams = root.GetProperty("params");
        templateParams.EnumerateObject().Select(property => property.Name)
            .Should()
            .Equal(
                new[] { "Name" },
                "la plantilla renderiza {{ params.Name }}: la clave va en params, PascalCase");
        templateParams.GetProperty("Name").GetString().Should()
            .Be(
                "Juan",
                "el saludo de la plantilla recibe sólo el primer nombre; el nombre completo va en to[].name");

        var senderElement = root.GetProperty("sender");
        senderElement.GetProperty("email").GetString().Should().Be(FromEmail);
        senderElement.GetProperty("name").GetString().Should().Be(FromName);
    }

    [Theory]
    [InlineData("Juan", "Juan")]
    [InlineData("Juan Diego Sánchez", "Juan")]
    [InlineData("  Juan   Diego  ", "Juan")]
    [InlineData("María", "María")]
    public async Task SendAsync_SendsOnlyTheFirstNameAsTemplateParam(
        string fullName,
        string expectedFirstName)
    {
        var handler = new StubHandler();
        handler.RespondWith(HttpStatusCode.Accepted);
        var sender = CreateSender(handler);

        await sender.SendAsync(new EmailMessage(RecipientEmail, fullName));

        using var payload = JsonDocument.Parse(handler.Requests[0].Body);
        var root = payload.RootElement;

        root.GetProperty("params").GetProperty("Name").GetString().Should()
            .Be(expectedFirstName, "el saludo de la plantilla debe mostrar sólo el primer nombre");

        root.GetProperty("to").EnumerateArray().Should().ContainSingle()
            .Which.GetProperty("name").GetString()
            .Should()
            .Be(fullName, "el destinatario del correo conserva el nombre completo");
    }

    [Fact]
    public async Task SendAsync_SendsNoSubjectNorHtml_TheTemplateOwnsTheContent()
    {
        var handler = new StubHandler();
        handler.RespondWith(HttpStatusCode.Accepted);
        var sender = CreateSender(handler);

        await sender.SendAsync(NewMessage());

        var body = handler.Requests[0].Body;
        body.Should()
            .NotContain("html")
            .And.NotContain("subject")
            .And.NotContain("<p>", "cero HTML en el backend: todo el contenido lo aporta la plantilla de Brevo");

        using var payload = JsonDocument.Parse(body);
        payload.RootElement.EnumerateObject().Select(property => property.Name)
            .Should()
            .Equal(
                new[] { "sender", "to", "templateId", "params" },
                "el payload sólo lleva remitente, destinatario, plantilla y parámetros");
    }

    [Fact]
    public async Task SendAsync_WhenBrevoFails_ThrowsADeliveryErrorNotADomainException()
    {
        var handler = new StubHandler();
        handler.RespondWith(
            HttpStatusCode.InternalServerError,
            "{\"code\":\"unavailable\",\"message\":\"temporary failure\"}");
        var sender = CreateSender(handler);

        var act = () => sender.SendAsync(NewMessage());

        var exception = await act.Should().ThrowAsync<EmailDeliveryException>(
            "un fallo de la API es un error de envío y lo decide el BackgroundService, no el handler");
        exception.Which.Message.Should().Contain("500", "el detalle explica qué respondió Brevo");
        exception.Which.Should()
            .NotBeAssignableTo<DomainException>(
                "las excepciones de dominio se mapean a respuestas con errorCode; el envío nunca debe entrar en ese mapeo");
    }

    [Fact]
    public async Task SendAsync_WhenBrevoIsUnreachable_ThrowsADeliveryError()
    {
        var handler = new StubHandler();
        handler.ThrowWith(new HttpRequestException("sin red"));
        var sender = CreateSender(handler);

        var act = () => sender.SendAsync(NewMessage());

        var exception = await act.Should().ThrowAsync<EmailDeliveryException>(
            "un fallo de transporte también es error de envío, no un error de dominio");
        exception.Which.Message.Should().Contain("Brevo");
    }

    [Theory]
    [InlineData("", 27, "Brevo:ApiKey")]
    [InlineData("xkeysib-test-key", 0, "Brevo:TemplateId")]
    public async Task SendAsync_WithoutAnEssentialSetting_ThrowsAClearConfigurationError(
        string apiKey,
        int templateId,
        string expectedSetting)
    {
        var handler = new StubHandler();
        var sender = CreateSender(
            handler,
            new EmailOptions
            {
                ApiKey = apiKey,
                FromEmail = FromEmail,
                FromName = FromName,
                TemplateId = templateId,
            });

        var act = () => sender.SendAsync(NewMessage());

        var exception = await act.Should().ThrowAsync<InvalidOperationException>(
            "sin ApiKey o sin TemplateId no hay envío posible: el error debe nombrar la setting que falta");
        exception.Which.Message.Should().Contain(expectedSetting);
        handler.CallCount.Should().Be(0, "la validación de configuración ocurre antes de llamar a la API");
    }
}

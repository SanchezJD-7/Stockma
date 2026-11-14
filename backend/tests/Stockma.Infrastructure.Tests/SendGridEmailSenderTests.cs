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

public class SendGridEmailSenderTests
{
    private const string ApiKey = "SG.test-key";
    private const string TemplateId = "d-11111111111111111111111111111111";
    private const string FromEmail = "hola@stockma.app";
    private const string FromName = "Stockma";
    private const string RecipientEmail = "juan@ejemplo.com";
    private const string RecipientName = "Juan Pérez";
    private const string SendGridMailEndpoint = "https://api.sendgrid.com/v3/mail/send";

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpResponseMessage>> results = new();
        private readonly List<(Uri Uri, string? Scheme, string? Parameter, string Body)> requests = new();

        public IReadOnlyList<(Uri Uri, string? Scheme, string? Parameter, string Body)> Requests => requests;

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
                request.Headers.Authorization?.Parameter,
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
    public async Task SendAsync_PostsTheTemplateIdAndTheNameAsDynamicTemplateData()
    {
        var handler = new StubHandler();
        handler.RespondWith(HttpStatusCode.Accepted);
        var sender = CreateSender(handler);

        await sender.SendAsync(NewMessage());

        var request = handler.Requests.Should().ContainSingle().Subject;
        request.Uri.Should()
            .Be(new Uri(SendGridMailEndpoint), "el envío usa la API mail/send de SendGrid");
        request.Scheme.Should().Be("Bearer", "SendGrid autentica con la API key en el header Authorization");
        request.Parameter.Should().Be(ApiKey);

        using var payload = JsonDocument.Parse(request.Body);
        var root = payload.RootElement;

        root.GetProperty("template_id").GetString()
            .Should()
            .Be(TemplateId, "el contenido vive en la dynamic template de la cuenta del usuario, no en el backend");

        var personalization = root.GetProperty("personalization").EnumerateArray().Should().ContainSingle().Subject;

        var to = personalization.GetProperty("to").EnumerateArray().Should().ContainSingle().Subject;
        to.GetProperty("email").GetString().Should().Be(RecipientEmail);
        to.GetProperty("name").GetString().Should().Be(RecipientName, "la plantilla recibe el nombre del destinatario");

        var templateData = personalization.GetProperty("dynamic_template_data");
        templateData.EnumerateObject().Select(property => property.Name)
            .Should()
            .Equal(
                new[] { "Name" },
                "la plantilla renderiza {{Name}}: la clave debe ir en PascalCase");
        templateData.GetProperty("Name").GetString().Should().Be(RecipientName);

        var from = root.GetProperty("from");
        from.GetProperty("email").GetString().Should().Be(FromEmail);
        from.GetProperty("name").GetString().Should().Be(FromName);
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
            .And.NotContain("<p>", "cero HTML en el backend: todo el contenido lo aporta la plantilla de SendGrid");

        using var payload = JsonDocument.Parse(body);
        payload.RootElement.EnumerateObject().Select(property => property.Name)
            .Should()
            .Equal(
                new[] { "personalization", "from", "template_id" },
                "el payload sólo lleva destinatario, remitente y plantilla");
    }

    [Fact]
    public async Task SendAsync_WhenSendGridFails_ThrowsADeliveryErrorNotADomainException()
    {
        var handler = new StubHandler();
        handler.RespondWith(
            HttpStatusCode.InternalServerError,
            "{\"errors\":[{\"message\":\"temporary failure\"}]}");
        var sender = CreateSender(handler);

        var act = () => sender.SendAsync(NewMessage());

        var exception = await act.Should().ThrowAsync<EmailDeliveryException>(
            "un fallo de la API es un error de envío y lo decide el BackgroundService, no el handler");
        exception.Which.Message.Should().Contain("500", "el detalle explica qué respondió SendGrid");
        exception.Which.Should()
            .NotBeAssignableTo<DomainException>(
                "las excepciones de dominio se mapean a respuestas con errorCode; el envío nunca debe entrar en ese mapeo");
    }

    [Fact]
    public async Task SendAsync_WhenSendGridIsUnreachable_ThrowsADeliveryError()
    {
        var handler = new StubHandler();
        handler.ThrowWith(new HttpRequestException("sin red"));
        var sender = CreateSender(handler);

        var act = () => sender.SendAsync(NewMessage());

        var exception = await act.Should().ThrowAsync<EmailDeliveryException>(
            "un fallo de transporte también es error de envío, no un error de dominio");
        exception.Which.Message.Should().Contain("SendGrid");
    }

    [Theory]
    [InlineData("", "d-11111111111111111111111111111111", "SendGrid:ApiKey")]
    [InlineData("SG.test-key", "", "SendGrid:TemplateId")]
    public async Task SendAsync_WithoutAnEssentialSetting_ThrowsAClearConfigurationError(
        string apiKey,
        string templateId,
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

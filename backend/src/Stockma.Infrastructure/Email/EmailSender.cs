using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stockma.Application.Waitlist;

namespace Stockma.Infrastructure.Email;

public sealed class EmailSender : IEmailSender
{
    private const string MailEndpoint = "https://api.brevo.com/v3/smtp/email";
    private const string ApiKeyHeaderName = "api-key";
    private const int MaximumDetailLength = 300;
    private readonly HttpClient httpClient;
    private readonly EmailOptions options;
    private readonly ILogger<EmailSender> logger;

    public EmailSender(HttpClient httpClient, IOptions<EmailOptions> options, ILogger<EmailSender> logger)
    {
        this.httpClient = httpClient;
        this.options = options.Value;
        this.logger = logger;
    }

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        try
        {
            using var response = await httpClient.SendAsync(BuildRequest(message), cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new EmailDeliveryException(Describe(response.StatusCode, body));
            }
        }
        catch (HttpRequestException exception)
        {
            throw new EmailDeliveryException($"Brevo no pudo ser contactado: {exception.Message}", exception);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new EmailDeliveryException($"Brevo no respondió a tiempo: {exception.Message}", exception);
        }

        logger.LogInformation(
            "Correo de confirmación enviado a {Email} con la plantilla {TemplateId}.",
            message.ToEmail,
            options.TemplateId);
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            throw new InvalidOperationException(EmailOptions.MissingApiKeyMessage);
        }

        if (options.TemplateId <= 0)
        {
            throw new InvalidOperationException(EmailOptions.MissingTemplateIdMessage);
        }
    }

    private HttpRequestMessage BuildRequest(EmailMessage message)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, MailEndpoint)
        {
            Content = JsonContent.Create(new BrevoMailRequest
            {
                To = [new BrevoRecipient { Email = message.ToEmail, Name = message.ToName }],
                Sender = new BrevoRecipient { Email = options.FromEmail, Name = options.FromName },
                TemplateId = options.TemplateId,
                Params = new Dictionary<string, string> { ["Name"] = FirstName(message.ToName) },
            }),
        };

        request.Headers.Add(ApiKeyHeaderName, options.ApiKey);

        return request;
    }

    private static string FirstName(string fullName)
    {
        var parts = fullName.Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return parts.Length > 0 ? parts[0] : fullName.Trim();
    }

    private static string Describe(HttpStatusCode statusCode, string body)
    {
        var detail = $"Brevo respondió {(int)statusCode} {statusCode}";

        if (string.IsNullOrWhiteSpace(body))
        {
            return detail;
        }

        var trimmed = body.Trim();

        return $"{detail}: {(trimmed.Length > MaximumDetailLength ? trimmed[..MaximumDetailLength] : trimmed)}";
    }

    private sealed class BrevoMailRequest
    {
        [JsonPropertyName("sender")]
        public required BrevoRecipient Sender { get; init; }

        [JsonPropertyName("to")]
        public required BrevoRecipient[] To { get; init; }

        [JsonPropertyName("templateId")]
        public required int TemplateId { get; init; }

        [JsonPropertyName("params")]
        public required Dictionary<string, string> Params { get; init; }
    }

    private sealed class BrevoRecipient
    {
        [JsonPropertyName("email")]
        public required string Email { get; init; }

        [JsonPropertyName("name")]
        public string? Name { get; init; }
    }
}

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stockma.Application.Waitlist;

namespace Stockma.Infrastructure.Email;

public sealed class EmailSender : IEmailSender
{
    private const string MailEndpoint = "https://api.sendgrid.com/v3/mail/send";
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
            throw new EmailDeliveryException($"SendGrid no pudo ser contactado: {exception.Message}", exception);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new EmailDeliveryException($"SendGrid no respondió a tiempo: {exception.Message}", exception);
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

        if (string.IsNullOrWhiteSpace(options.TemplateId))
        {
            throw new InvalidOperationException(EmailOptions.MissingTemplateIdMessage);
        }
    }

    private HttpRequestMessage BuildRequest(EmailMessage message)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, MailEndpoint)
        {
            Content = JsonContent.Create(new SendGridMailRequest
            {
                Personalization =
                [
                    new SendGridPersonalization
                    {
                        To = [new SendGridRecipient { Email = message.ToEmail, Name = message.ToName }],
                        DynamicTemplateData = new Dictionary<string, string> { ["Name"] = message.ToName },
                    },
                ],
                From = new SendGridRecipient { Email = options.FromEmail, Name = options.FromName },
                TemplateId = options.TemplateId,
            }),
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);

        return request;
    }

    private static string Describe(HttpStatusCode statusCode, string body)
    {
        var detail = $"SendGrid respondió {(int)statusCode} {statusCode}";

        if (string.IsNullOrWhiteSpace(body))
        {
            return detail;
        }

        var trimmed = body.Trim();

        return $"{detail}: {(trimmed.Length > MaximumDetailLength ? trimmed[..MaximumDetailLength] : trimmed)}";
    }

    private sealed class SendGridMailRequest
    {
        [JsonPropertyName("personalization")]
        public required SendGridPersonalization[] Personalization { get; init; }

        [JsonPropertyName("from")]
        public required SendGridRecipient From { get; init; }

        [JsonPropertyName("template_id")]
        public required string TemplateId { get; init; }
    }

    private sealed class SendGridPersonalization
    {
        [JsonPropertyName("to")]
        public required SendGridRecipient[] To { get; init; }

        [JsonPropertyName("dynamic_template_data")]
        public required Dictionary<string, string> DynamicTemplateData { get; init; }
    }

    private sealed class SendGridRecipient
    {
        [JsonPropertyName("email")]
        public required string Email { get; init; }

        [JsonPropertyName("name")]
        public string? Name { get; init; }
    }
}

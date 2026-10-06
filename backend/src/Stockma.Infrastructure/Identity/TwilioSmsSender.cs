using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stockma.Application.Identity;

namespace Stockma.Infrastructure.Identity;

public sealed class TwilioSmsSender : ISmsSender
{
    private const string MessagesUriFormat = "https://api.twilio.com/2010-04-01/Accounts/{0}/Messages.json";

    private readonly HttpClient httpClient;
    private readonly SmsOptions options;
    private readonly ILogger<TwilioSmsSender> logger;

    public TwilioSmsSender(HttpClient httpClient, IOptions<SmsOptions> options, ILogger<TwilioSmsSender> logger)
    {
        this.httpClient = httpClient;
        this.options = options.Value;
        this.logger = logger;
    }

    public async Task SendAsync(string phoneNumber, string message, CancellationToken cancellationToken = default)
    {
        var attempts = options.RetryCount + 1;

        for (var attempt = 1; ; attempt++)
        {
            var outcome = await SendOnceAsync(phoneNumber, message, cancellationToken);

            if (outcome.Success)
            {
                return;
            }

            if (!outcome.Transient || attempt >= attempts)
            {
                throw new SmsDeliveryException(outcome.Detail);
            }

            logger.LogWarning(
                "Envío de SMS por Twilio fallido, intento {Attempt} de {Attempts} hacia {PhoneNumber}: {Detail}",
                attempt,
                attempts,
                Mask(phoneNumber),
                outcome.Detail);

            await Task.Delay(TimeSpan.FromMilliseconds(options.RetryDelayMs * attempt), cancellationToken);
        }
    }

    private async Task<Outcome> SendOnceAsync(string phoneNumber, string message, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.SendAsync(BuildRequest(phoneNumber, message), cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation("SMS enviado por Twilio hacia {PhoneNumber}.", Mask(phoneNumber));
                return Outcome.Succeeded;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            return new Outcome(false, IsTransient(response.StatusCode), Describe(response.StatusCode, body));
        }
        catch (HttpRequestException exception)
        {
            return new Outcome(false, true, $"Twilio no pudo ser contactado: {exception.Message}");
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            return new Outcome(false, true, $"Twilio no respondió a tiempo: {exception.Message}");
        }
    }

    private HttpRequestMessage BuildRequest(string phoneNumber, string message)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            string.Format(MessagesUriFormat, options.AccountSid))
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["To"] = phoneNumber,
                ["From"] = options.Sender,
                ["Body"] = message,
            }),
        };

        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{options.AccountSid}:{options.ApiKey}")));

        return request;
    }

    private static bool IsTransient(HttpStatusCode statusCode) =>
        (int)statusCode is >= 500 or 408 or 429;

    private static string Describe(HttpStatusCode statusCode, string body)
    {
        var detail = $"Twilio respondió {(int)statusCode} {statusCode}";

        if (string.IsNullOrWhiteSpace(body))
        {
            return detail;
        }

        var trimmed = body.Trim();

        return $"{detail}: {(trimmed.Length > 300 ? trimmed[..300] : trimmed)}";
    }

    private static string Mask(string phoneNumber) =>
        phoneNumber.Length < 9
            ? "***"
            : $"{phoneNumber[..4]}{new string('*', phoneNumber.Length - 7)}{phoneNumber[^3..]}";

    private readonly record struct Outcome(bool Success, bool Transient, string Detail)
    {
        public static readonly Outcome Succeeded = new(true, false, string.Empty);
    }
}

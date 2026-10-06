using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Stockma.Application.Identity;
using Stockma.Infrastructure.Identity;

namespace Stockma.Infrastructure.Tests;

public class TwilioSmsSenderTests
{
    private const string AccountSid = "AC00000000000000000000000000000000";
    private const string ApiKey = "test-secret";
    private const string SenderNumber = "+5491155551234";
    private const string PhoneNumber = "+573001234567";
    private const string Message = "Tu código de acceso a Stockma es 483920. Vence en 10 minutos.";

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpResponseMessage>> results = new();
        private readonly List<(Uri Uri, string? Scheme, string? Parameter, string Body)> requests = new();

        public IReadOnlyList<(Uri Uri, string? Scheme, string? Parameter, string Body)> Requests => requests;

        public int CallCount => requests.Count;

        public Action? OnSend { get; set; }

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

            OnSend?.Invoke();

            if (results.Count == 0)
            {
                throw new InvalidOperationException("El test no preparó una respuesta para este llamado.");
            }

            return results.Dequeue()();
        }
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        private readonly List<(LogLevel Level, string Message)> entries = new();

        public IReadOnlyList<(LogLevel Level, string Message)> Entries => entries;

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            entries.Add((logLevel, formatter(state, exception)));
    }

    private static SmsOptions NewOptions(int retryCount = 2, int retryDelayMs = 0) =>
        new()
        {
            Provider = SmsOptions.TwilioProvider,
            AccountSid = AccountSid,
            ApiKey = ApiKey,
            Sender = SenderNumber,
            RetryCount = retryCount,
            RetryDelayMs = retryDelayMs,
        };

    private static TwilioSmsSender CreateSender(
        StubHandler handler,
        SmsOptions? options = null,
        ILogger<TwilioSmsSender>? logger = null) =>
        new(
            new HttpClient(handler),
            Options.Create(options ?? NewOptions()),
            logger ?? NullLogger<TwilioSmsSender>.Instance);

    [Fact]
    public async Task SendAsync_PostsFormEncodedToTwilioWithBasicAuth()
    {
        var handler = new StubHandler();
        handler.RespondWith(HttpStatusCode.Created, "{\"sid\":\"SM123\"}");
        var sender = CreateSender(handler);

        await sender.SendAsync(PhoneNumber, Message);

        var request = handler.Requests.Should().ContainSingle().Subject;
        request.Uri.Should()
            .Be(
                new Uri($"https://api.twilio.com/2010-04-01/Accounts/{AccountSid}/Messages.json"),
                "T049: el sender habla con la API Messages de Twilio sobre la cuenta configurada");
        request.Scheme.Should().Be("Basic", "Twilio autentica con el SID de cuenta y el auth token");
        request.Parameter.Should().Be(Convert.ToBase64String(Encoding.UTF8.GetBytes($"{AccountSid}:{ApiKey}")));
        request.Body.Should()
            .Contain($"To={Uri.EscapeDataString(PhoneNumber)}")
            .And.Contain($"From={Uri.EscapeDataString(SenderNumber)}")
            .And.Contain("483920");
    }

    [Fact]
    public async Task SendAsync_RetriesATransientFailureAndSucceeds()
    {
        var handler = new StubHandler();
        handler.RespondWith(HttpStatusCode.InternalServerError, "{\"message\":\"boom\"}");
        handler.RespondWith(HttpStatusCode.Created, "{\"sid\":\"SM123\"}");
        var sender = CreateSender(handler);

        var act = () => sender.SendAsync(PhoneNumber, Message);

        await act.Should().NotThrowAsync("un 5xx es transitorio: el envío se reintenta antes de rendirse");
        handler.CallCount.Should().Be(2);
    }

    [Fact]
    public async Task SendAsync_WhenTheRetriesRunOut_ThrowsSmsDeliveryException()
    {
        var handler = new StubHandler();
        handler.RespondWith(HttpStatusCode.ServiceUnavailable, "{\"message\":\"boom\"}");
        handler.RespondWith(HttpStatusCode.ServiceUnavailable, "{\"message\":\"boom\"}");
        var sender = CreateSender(handler, NewOptions(retryCount: 1));

        var act = () => sender.SendAsync(PhoneNumber, Message);

        var exception = await act.Should().ThrowAsync<SmsDeliveryException>();
        exception.Which.Message.Should().Contain("503");
        handler.CallCount.Should().Be(2, "un intento original más un reintento");
    }

    [Fact]
    public async Task SendAsync_OnAPermanentClientError_DoesNotRetry()
    {
        var handler = new StubHandler();
        handler.RespondWith(
            HttpStatusCode.BadRequest,
            "{\"code\":21211,\"message\":\"The To number is not a valid phone number.\"}");
        var sender = CreateSender(handler);

        var act = () => sender.SendAsync(PhoneNumber, Message);

        var exception = await act.Should().ThrowAsync<SmsDeliveryException>();
        exception.Which.Message.Should().Contain("21211", "el detalle de Twilio explica por qué no se mandó");
        handler.CallCount.Should().Be(1, "un 4xx no es transitorio: reintentarlo sólo quema la cuota");
    }

    [Fact]
    public async Task SendAsync_WhenTheRequestTimesOut_IsRetriedAsATransientFailure()
    {
        var handler = new StubHandler();
        handler.ThrowWith(new TaskCanceledException("timeout"));
        var sender = CreateSender(handler, NewOptions(retryCount: 0));

        var act = () => sender.SendAsync(PhoneNumber, Message);

        var exception = await act.Should().ThrowAsync<SmsDeliveryException>(
            "un timeout no lo pidió el caller: es un fallo transitorio del proveedor");
        exception.Which.Message.Should().Contain("no respondió a tiempo");
        handler.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task SendAsync_WhenTheCallerCancels_DoesNotRetry()
    {
        using var cancellation = new CancellationTokenSource();
        var handler = new StubHandler { OnSend = cancellation.Cancel };
        handler.ThrowWith(new TaskCanceledException("cancelado por el caller"));
        var sender = CreateSender(handler, NewOptions(retryCount: 3));

        var act = () => sender.SendAsync(PhoneNumber, Message, cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>(
            "la cancelación del caller no es un fallo del proveedor: no se reintenta");
        handler.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task SendAsync_LogsTheDestinationMasked()
    {
        var handler = new StubHandler();
        handler.RespondWith(HttpStatusCode.Created);
        var logger = new CapturingLogger<TwilioSmsSender>();
        var sender = CreateSender(handler, logger: logger);

        await sender.SendAsync(PhoneNumber, Message);

        logger.Entries.Select(entry => entry.Message).Should()
            .ContainSingle(message => message.Contains('+'))
            .Which.Should()
            .NotContain(PhoneNumber, "los logs de producción no guardan el celular completo")
            .And.Contain("+573", "el enmascarado conserva suficiente para cruzar con el proveedor");
    }
}

using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Stockma.Application.Waitlist;
using Stockma.Infrastructure.Email;

namespace Stockma.Infrastructure.Tests;

public class EmailBackgroundServiceTests
{
    [Fact]
    public async Task ExecuteAsync_SendsEveryEnqueuedMessageThroughTheSender()
    {
        var queue = new EmailQueue();
        var sender = new FakeEmailSender();
        var bothDelivered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        sender.OnSent = () =>
        {
            if (sender.Sent.Count == 2)
            {
                bothDelivered.SetResult();
            }
        };

        var provider = CreateProvider(sender);
        using var service = new EmailBackgroundService(
            queue,
            provider,
            NullLogger<EmailBackgroundService>.Instance);

        await service.StartAsync(CancellationToken.None);
        queue.Enqueue(new EmailMessage("juan@ejemplo.com", "Juan Pérez"));
        queue.Enqueue(new EmailMessage("ana@ejemplo.com", "Ana Gómez"));

        await bothDelivered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        sender.Sent.Should().HaveCount(2);
        sender.Sent[0].ToEmail.Should().Be("juan@ejemplo.com");
        sender.Sent[0].ToName.Should().Be("Juan Pérez");
        sender.Sent[1].ToEmail.Should().Be("ana@ejemplo.com");
        sender.Sent[1].ToName.Should().Be("Ana Gómez");

        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheSendFails_LogsTheErrorAndKeepsDraining()
    {
        var queue = new EmailQueue();
        var sender = new FakeEmailSender { FailuresRemaining = 1 };
        var logger = new CapturingLogger<EmailBackgroundService>();
        var secondDelivered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        sender.OnSent = secondDelivered.SetResult;

        var provider = CreateProvider(sender);
        using var service = new EmailBackgroundService(queue, provider, logger);

        await service.StartAsync(CancellationToken.None);
        queue.Enqueue(new EmailMessage("juan@ejemplo.com", "Juan Pérez"));
        queue.Enqueue(new EmailMessage("ana@ejemplo.com", "Ana Gómez"));

        await secondDelivered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        sender.Sent.Should()
            .ContainSingle()
            .Which.ToEmail.Should().Be("ana@ejemplo.com", "el primer envío falló, el segundo sí se mandó");

        var warning = logger.Entries.Should()
            .ContainSingle(entry => entry.Level == LogLevel.Warning)
            .Which;
        warning.Message.Should().Contain("juan@ejemplo.com", "el log identifica el correo que no se pudo enviar");
        warning.Exception.Should()
            .BeOfType<EmailDeliveryException>("el log conserva la causa real para diagnosticarla");

        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheQueueReaderFails_LogsAndEndsInsteadOfPropagating()
    {
        var logger = new CapturingLogger<EmailBackgroundService>();
        using var service = new EmailBackgroundService(
            new ThrowingEmailQueue(),
            CreateProvider(new FakeEmailSender()),
            logger);

        Func<Task> act = () => service.StartAsync(CancellationToken.None);

        await act.Should()
            .NotThrowAsync("un fallo del proceso nunca puede tumbar el host ni la respuesta 201");
        logger.Entries.Should()
            .ContainSingle(entry => entry.Level == LogLevel.Error)
            .Which.Message.Should().Contain("cola de correo");

        await service.StopAsync(CancellationToken.None);
    }

    private static IServiceScopeFactory CreateProvider(IEmailSender sender)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => sender);
        var provider = services.BuildServiceProvider();

        return provider.GetRequiredService<IServiceScopeFactory>();
    }

    private sealed class ThrowingEmailQueue : IEmailQueue
    {
        public void Enqueue(EmailMessage message)
        {
        }

        public IAsyncEnumerable<EmailMessage> ReadAllAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("La cola de correo dejó de leer.");
    }

    private sealed class FakeEmailSender : IEmailSender
    {
        public List<EmailMessage> Sent { get; } = [];
        public int FailuresRemaining { get; set; }
        public Action? OnSent { get; set; }

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            if (FailuresRemaining > 0)
            {
                FailuresRemaining--;
                throw new EmailDeliveryException("Brevo respondió 503 ServiceUnavailable");
            }

            Sent.Add(message);
            OnSent?.Invoke();

            return Task.CompletedTask;
        }
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception), exception));
    }
}

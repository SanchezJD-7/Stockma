using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Stockma.Application.Waitlist;

namespace Stockma.Infrastructure.Email;

public sealed class EmailBackgroundService(
    IEmailQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<EmailBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var message in queue.ReadAllAsync(stoppingToken))
            {
                await SendSafelyAsync(message, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "El drenaje de la cola de correo se detuvo por un error inesperado; el registro no se ve afectado.");
        }
    }

    private async Task SendSafelyAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
            await sender.SendAsync(message, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "No se pudo enviar el correo de confirmación a {Email}; el registro ya quedó persistido.",
                message.ToEmail);
        }
    }
}

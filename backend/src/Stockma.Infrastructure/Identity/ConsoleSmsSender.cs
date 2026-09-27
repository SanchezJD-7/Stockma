using Microsoft.Extensions.Logging;
using Stockma.Application.Identity;

namespace Stockma.Infrastructure.Identity;

public sealed class ConsoleSmsSender(ILogger<ConsoleSmsSender> logger) : ISmsSender
{
    public Task SendAsync(string phoneNumber, string message, CancellationToken cancellationToken = default)
    {
        logger.LogWarning(
            "[SMS DE DESARROLLO] Para {PhoneNumber}: {Message}. No se envió nada: no hay proveedor configurado.",
            phoneNumber,
            message);

        return Task.CompletedTask;
    }
}

using Stockma.Application.Identity;

namespace Stockma.Infrastructure.Identity;

public sealed class DisabledSmsSender : ISmsSender
{
    public Task SendAsync(string phoneNumber, string message, CancellationToken cancellationToken = default) =>
        Task.FromException(new SmsDeliveryException(SmsOptions.NotConfiguredMessage));
}

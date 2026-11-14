namespace Stockma.Application.Waitlist;

public interface IEmailQueue
{
    void Enqueue(EmailMessage message);

    IAsyncEnumerable<EmailMessage> ReadAllAsync(CancellationToken cancellationToken = default);
}

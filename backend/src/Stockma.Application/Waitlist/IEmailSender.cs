namespace Stockma.Application.Waitlist;

public sealed record EmailMessage(string ToEmail, string ToName);

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}

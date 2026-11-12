namespace Stockma.Application.Waitlist;

public sealed record EmailMessage(string ToEmail, string ToName, string Subject, string HtmlBody);

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}

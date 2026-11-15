namespace OpenTicket.Infrastructure.Email;

public sealed record EmailMessage(string To, string Subject, string TextBody);

/// <summary>Sends transactional email. Implementations throw on delivery failure.</summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken ct);
}

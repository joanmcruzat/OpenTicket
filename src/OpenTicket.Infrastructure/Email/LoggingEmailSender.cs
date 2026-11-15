using Microsoft.Extensions.Logging;

namespace OpenTicket.Infrastructure.Email;

/// <summary>Development stand-in for SMTP: writes the message to the log. Enabled only by Email:LogOnly.</summary>
internal sealed partial class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        LogMessage(message.Subject, message.TextBody);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Email (not sent, Email:LogOnly is on): {Subject}\n{Body}")]
    private partial void LogMessage(string subject, string body);
}

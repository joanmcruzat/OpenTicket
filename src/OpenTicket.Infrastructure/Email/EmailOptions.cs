using System.ComponentModel.DataAnnotations;

namespace OpenTicket.Infrastructure.Email;

/// <summary>
/// Outgoing mail. Leave <see cref="SmtpHost"/> empty to disable email (password reset is then turned off), or set
/// <see cref="LogOnly"/> in development to write messages to the log instead of sending them.
/// Any SMTP provider works; Brevo and Resend both have free tiers.
/// </summary>
public sealed class EmailOptions
{
    public const string Section = "Email";

    public string? SmtpHost { get; init; }

    [Range(1, 65535)]
    public int SmtpPort { get; init; } = 587;

    public string? SmtpUsername { get; init; }

    public string? SmtpPassword { get; init; }

    /// <summary>Sender address, for example "OpenTicket &lt;no-reply@example.com&gt;".</summary>
    public string? From { get; init; }

    /// <summary>Development only: log messages instead of sending them. Never enable in production (logs would hold reset links).</summary>
    public bool LogOnly { get; init; }

    public bool IsEnabled => LogOnly || !string.IsNullOrWhiteSpace(SmtpHost);
}

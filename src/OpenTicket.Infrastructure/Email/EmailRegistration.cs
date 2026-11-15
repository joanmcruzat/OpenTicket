using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace OpenTicket.Infrastructure.Email;

internal static class EmailRegistration
{
    public static IServiceCollection AddOpenTicketEmail(this IServiceCollection services)
    {
        services.AddOptions<EmailOptions>()
            .BindConfiguration(EmailOptions.Section)
            .ValidateDataAnnotations()
            .Validate(o => o.LogOnly || string.IsNullOrWhiteSpace(o.SmtpHost) || !string.IsNullOrWhiteSpace(o.From),
                "Email:From is required when Email:SmtpHost is set.")
            .ValidateOnStart();

        services.AddSingleton<IEmailSender>(sp => sp.GetRequiredService<IOptions<EmailOptions>>().Value.LogOnly
            ? ActivatorUtilities.CreateInstance<LoggingEmailSender>(sp)
            : ActivatorUtilities.CreateInstance<SmtpEmailSender>(sp));
        return services;
    }
}

using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Threading.RateLimiting;

using Microsoft.Extensions.Options;

namespace OpenTicket.API.Security;

internal sealed class RateLimitingOptions
{
    public const string Section = "RateLimiting";

    /// <summary>Requests per client IP per minute on sign-up, sign-in and token refresh.</summary>
    [Range(1, 100_000)]
    public int AuthPermitLimit { get; init; } = 10;
}

internal static class RateLimiting
{
    public const string AuthPolicy = "auth";

    public static IServiceCollection AddOpenTicketRateLimiting(this IServiceCollection services)
    {
        services.AddOptions<RateLimitingOptions>()
            .BindConfiguration(RateLimitingOptions.Section)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.OnRejected = (context, _) =>
            {
                context.HttpContext.Response.Headers.RetryAfter = 60.ToString(CultureInfo.InvariantCulture);
                return ValueTask.CompletedTask;
            };
            o.AddPolicy(AuthPolicy, context =>
            {
                var limit = context.RequestServices.GetRequiredService<IOptions<RateLimitingOptions>>().Value.AuthPermitLimit;
                return RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = limit,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    });
            });
        });
        return services;
    }
}

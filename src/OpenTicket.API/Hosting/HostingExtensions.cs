using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

using OpenTicket.API.ErrorHandling;
using OpenTicket.Domain.Common;
using OpenTicket.Infrastructure.Persistence;

namespace OpenTicket.API.Hosting;

internal static class HostingExtensions
{
    /// <summary>
    /// Behind a TLS-terminating proxy (Render, Caddy, nginx) the client IP and scheme arrive in X-Forwarded-*.
    /// ForwardLimit stays 1: only the entry added by the nearest proxy is trusted, so clients cannot spoof it.
    /// </summary>
    public static IServiceCollection AddProxySupport(this IServiceCollection services, IConfiguration configuration)
    {
        if (configuration.GetValue<bool>("ForwardedHeaders:Enabled"))
        {
            services.Configure<ForwardedHeadersOptions>(o =>
            {
                o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                o.KnownIPNetworks.Clear();
                o.KnownProxies.Clear();
            });
        }

        return services;
    }

    /// <summary>`dotnet OpenTicket.API.dll --migrate` applies pending migrations and exits (self-hosted deployments).</summary>
    public static bool IsMigrateCommand(string[] args) => args.Contains("--migrate", StringComparer.OrdinalIgnoreCase);

    public static async Task MigrateDatabaseAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<OpenTicketDbContext>().Database.MigrateAsync();
    }

    /// <summary>
    /// Serves the Angular build from wwwroot with an index.html fallback for client-side routes.
    /// Unknown /api paths still get a ProblemDetails 404 instead of the SPA shell.
    /// </summary>
    public static void MapSpa(this WebApplication app)
    {
        // Public by design: unknown API routes must answer 404 to anyone, not 401.
        app.Map("/api/{**path}", () => Error.NotFound("route.not_found", "The requested API route does not exist.").ToProblem())
            .AllowAnonymous()
            .ExcludeFromDescription();

        // Public by design: the SPA shell and assets contain no data; every API call still requires a token.
        app.MapFallbackToFile("index.html").AllowAnonymous();
    }
}

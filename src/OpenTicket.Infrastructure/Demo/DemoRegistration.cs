using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using OpenTicket.Application.Auth;

namespace OpenTicket.Infrastructure.Demo;

public static class DemoRegistration
{
    internal static IServiceCollection AddOpenTicketDemo(this IServiceCollection services)
    {
        services.AddOptions<DemoOptions>().BindConfiguration(DemoOptions.Section).ValidateDataAnnotations().ValidateOnStart();
        services.AddScoped<DemoDataSeeder>();
        services.AddScoped<IDemoService, DemoService>();
        services.AddHostedService<DemoResetService>();
        return services;
    }

    /// <summary>Startup: in demo mode, make sure the demo cast and sample tickets exist.</summary>
    public static async Task SeedDemoAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        if (!services.GetRequiredService<IOptions<DemoOptions>>().Value.Enabled)
        {
            return;
        }

        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DemoDataSeeder>().EnsureSeededAsync(ct);
    }
}

/// <summary>Restores the demo data every Demo:ResetIntervalHours while demo mode is on.</summary>
internal sealed partial class DemoResetService(
    IOptions<DemoOptions> options,
    IServiceScopeFactory scopes,
    TimeProvider clock,
    ILogger<DemoResetService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.Enabled || settings.ResetIntervalHours == 0)
        {
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromHours(settings.ResetIntervalHours), clock);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<DemoDataSeeder>().ResetAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Keep the timer alive: one failed reset (e.g. the database blipped) must not stop future resets.
                LogResetFailed(ex);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Demo data reset failed; will retry at the next interval")]
    private partial void LogResetFailed(Exception exception);
}

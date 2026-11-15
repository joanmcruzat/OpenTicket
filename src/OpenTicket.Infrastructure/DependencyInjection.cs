using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using OpenTicket.Application.Common;

using OpenTicket.Infrastructure.Demo;
using OpenTicket.Infrastructure.Email;
using OpenTicket.Infrastructure.Identity;
using OpenTicket.Infrastructure.Persistence;

namespace OpenTicket.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "Default";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{ConnectionStringName}' is not configured. " +
                "Set ConnectionStrings__Default (environment) or use dotnet user-secrets.");
        }

        services.AddDbContext<OpenTicketDbContext>(options => options
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention());

        services.AddScoped<IOpenTicketDbContext>(sp => sp.GetRequiredService<OpenTicketDbContext>());
        services.AddScoped<IUserDirectory, UserDirectory>();

        services.TryAddSingleton(TimeProvider.System);
        services.AddOpenTicketIdentity();
        services.AddOpenTicketEmail();
        services.AddOpenTicketDemo();

        services.AddHealthChecks()
            .AddDbContextCheck<OpenTicketDbContext>("database", tags: ["ready"]);

        return services;
    }
}

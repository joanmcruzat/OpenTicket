using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OpenTicket.Infrastructure.Persistence;

/// <summary>
/// Lets `dotnet ef` (migrations, bundles) build the model without starting the app or reading secrets.
/// The connection string is only a placeholder; bundles receive the real one via --connection.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<OpenTicketDbContext>
{
    public OpenTicketDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Host=localhost;Database=openticket_design_time";
        var options = new DbContextOptionsBuilder<OpenTicketDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        return new OpenTicketDbContext(options);
    }
}

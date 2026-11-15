using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using OpenTicket.Domain.Users;

namespace OpenTicket.Infrastructure.Identity;

public static partial class IdentitySeeder
{
    /// <summary>Ensures the three roles exist and bootstraps the first admin from configuration.</summary>
    public static async Task SeedIdentityAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var roles = provider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var users = provider.GetRequiredService<UserManager<ApplicationUser>>();
        var seed = provider.GetRequiredService<IOptions<SeedOptions>>().Value;
        var clock = provider.GetRequiredService<TimeProvider>();
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(IdentitySeeder));

        foreach (var role in Enum.GetNames<UserRole>())
        {
            ct.ThrowIfCancellationRequested();
            if (!await roles.RoleExistsAsync(role))
            {
                EnsureSucceeded(await roles.CreateAsync(new IdentityRole<Guid>(role) { Id = Guid.CreateVersion7() }), "create role");
            }
        }

        if ((await users.GetUsersInRoleAsync(nameof(UserRole.Admin))).Count > 0)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(seed.AdminEmail) || string.IsNullOrWhiteSpace(seed.AdminPassword))
        {
            LogNoAdmin(logger);
            return;
        }

        var admin = new ApplicationUser
        {
            UserName = seed.AdminEmail,
            Email = seed.AdminEmail,
            DisplayName = seed.AdminDisplayName,
            CreatedAtUtc = clock.GetUtcNow().UtcDateTime,
        };
        EnsureSucceeded(await users.CreateAsync(admin, seed.AdminPassword), "create the seed admin");
        EnsureSucceeded(await users.AddToRoleAsync(admin, nameof(UserRole.Admin)), "assign the Admin role");
        LogAdminCreated(logger, admin.Id);
    }

    private static void EnsureSucceeded(IdentityResult result, string action)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Identity seeding failed to {action}: {result}");
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "No admin exists and Seed:AdminEmail/Seed:AdminPassword are not set; nobody can manage users yet")]
    private static partial void LogNoAdmin(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Seed admin {UserId} created")]
    private static partial void LogAdminCreated(ILogger logger, Guid userId);
}

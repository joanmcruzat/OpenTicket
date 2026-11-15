using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

using OpenTicket.Application.Auth;
using OpenTicket.Domain.Common;
using OpenTicket.Domain.Users;
using OpenTicket.Infrastructure.Identity;

namespace OpenTicket.Infrastructure.Demo;

internal sealed class DemoService(
    IOptions<DemoOptions> options,
    UserManager<ApplicationUser> users,
    AuthService auth) : IDemoService
{
    public static readonly Error Disabled = Error.NotFound("demo.disabled", "This server is not running as a demo.");

    public DemoInfo? Info => options.Value.Enabled ? new DemoInfo(options.Value.ResetIntervalHours) : null;

    public async Task<Result<AuthSession>> SignInAsync(UserRole role, CancellationToken ct)
    {
        if (!options.Value.Enabled)
        {
            return Disabled;
        }

        var user = await users.FindByEmailAsync(DemoAccounts.SignInAs(role).Email);
        if (user is null || !user.IsActive)
        {
            // Demo accounts are created at startup and protected from changes; reaching this is a setup bug.
            throw new InvalidOperationException($"The demo {role} account is missing or inactive.");
        }

        return await auth.StartSessionAsync(user, ct);
    }
}

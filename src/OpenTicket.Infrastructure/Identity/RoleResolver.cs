using Microsoft.AspNetCore.Identity;

using OpenTicket.Domain.Users;

namespace OpenTicket.Infrastructure.Identity;

internal static class RoleResolver
{
    /// <summary>Returns the user's single role. Users created through the API always have exactly one.</summary>
    public static async Task<UserRole> GetRoleAsync(this UserManager<ApplicationUser> users, ApplicationUser user)
    {
        var roles = await users.GetRolesAsync(user);
        return roles.Count == 1 && Enum.TryParse<UserRole>(roles[0], out var role)
            ? role
            : throw new InvalidOperationException($"User {user.Id} must have exactly one known role.");
    }
}

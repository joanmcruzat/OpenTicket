using OpenTicket.Domain.Users;

namespace OpenTicket.API.Security;

internal static class Policies
{
    public const string AdminOnly = nameof(AdminOnly);
    public const string StaffOnly = nameof(StaffOnly);

    public static IServiceCollection AddOpenTicketAuthorization(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            // Secure by default: every endpoint needs a valid token unless it explicitly allows anonymous access.
            .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser().Build())
            .AddPolicy(AdminOnly, p => p.RequireRole(nameof(UserRole.Admin)))
            .AddPolicy(StaffOnly, p => p.RequireRole(nameof(UserRole.Admin), nameof(UserRole.Agent)));
        return services;
    }
}

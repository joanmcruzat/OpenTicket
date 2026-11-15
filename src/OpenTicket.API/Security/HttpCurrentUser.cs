using System.Security.Claims;

using Microsoft.IdentityModel.JsonWebTokens;

using OpenTicket.Application.Common;
using OpenTicket.Domain.Users;
using OpenTicket.Infrastructure.Identity;

namespace OpenTicket.API.Security;

internal sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Actor Actor
    {
        get
        {
            var principal = accessor.HttpContext?.User;
            var subject = principal?.FindFirstValue(JwtRegisteredClaimNames.Sub);
            var role = principal?.FindFirstValue(OpenTicketClaims.Role);
            return Guid.TryParse(subject, out var userId) && Enum.TryParse<UserRole>(role, out var userRole)
                ? new Actor(userId, userRole)
                : throw new InvalidOperationException("ICurrentUser was used on a request without an authenticated user.");
        }
    }
}

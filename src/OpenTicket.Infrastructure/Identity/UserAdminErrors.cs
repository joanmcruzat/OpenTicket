using OpenTicket.Domain.Common;

namespace OpenTicket.Infrastructure.Identity;

internal static class UserAdminErrors
{
    public static readonly Error NotFound = Error.NotFound("user.not_found", "The user was not found.");

    // Self-protection also guarantees at least one active admin: the caller is always an active admin.
    public static readonly Error CannotChangeOwnRole =
        Error.Forbidden("user.self_role_change", "You cannot change your own role.");

    public static readonly Error CannotDeactivateSelf =
        Error.Forbidden("user.self_deactivate", "You cannot deactivate your own account.");

    public static readonly Error HasOpenAssignments = Error.Conflict(
        "user.has_assignments", "Reassign this agent's open tickets before changing their role.");

    public static readonly Error DemoAccountProtected = Error.Forbidden(
        "user.demo_protected", "Demo accounts can't be changed, so every visitor gets the same demo. Create a new user to try this.");
}

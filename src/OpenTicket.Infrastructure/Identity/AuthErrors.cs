using OpenTicket.Application.Common;
using OpenTicket.Domain.Common;

namespace OpenTicket.Infrastructure.Identity;

internal static class AuthErrors
{
    // One generic message for every login failure, so callers cannot probe which accounts exist or are locked.
    public static readonly Error InvalidCredentials =
        Error.Unauthorized("auth.invalid_credentials", "Invalid email or password.");

    public static readonly Error InvalidRefreshToken =
        Error.Unauthorized("auth.invalid_refresh_token", "Your session has expired. Please sign in again.");

    public static readonly Error EmailTaken =
        Error.Conflict("auth.email_taken", "An account with this email already exists.");

    public static readonly Error UserNotFound =
        Error.NotFound("user.not_found", "The user was not found.");

    public static readonly Error PasswordResetDisabled = Error.BusinessRule(
        "auth.password_reset_disabled", "Password reset by email is not set up. Ask an admin to reset your password.");

    public static readonly Error InvalidResetLink = Error.Validation(
        "auth.invalid_reset_link", "This reset link is invalid or has expired. Request a new one.");

    public static readonly Error ExternalEmailUnverified = Error.Unauthorized(
        "auth.external_email_unverified",
        "Your account with this provider has no verified email address. Verify it there, or sign up with email.");

    public static readonly Error AccountDisabled =
        Error.Unauthorized("auth.account_disabled", "This account is disabled. Contact an admin.");

    public static readonly Error RegistrationDisabled = Error.Forbidden(
        "auth.registration_disabled", "Sign-up is turned off on this demo. Use one of the demo accounts instead.");
}

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using OpenTicket.Application.Auth;
using OpenTicket.Domain.Common;
using OpenTicket.Infrastructure.Demo;
using OpenTicket.Infrastructure.Email;

namespace OpenTicket.Infrastructure.Identity;

internal sealed partial class AccountRecoveryService(
    UserManager<ApplicationUser> users,
    AuthService auth,
    IEmailSender email,
    IOptions<EmailOptions> emailOptions,
    IOptions<AppOptions> appOptions,
    ILogger<AccountRecoveryService> logger) : IAccountRecoveryService
{
    public bool IsEnabled => emailOptions.Value.IsEnabled && appOptions.Value.HasValidPublicUrl;

    public async Task<Result> RequestPasswordResetAsync(string emailAddress, CancellationToken ct)
    {
        if (!IsEnabled)
        {
            return AuthErrors.PasswordResetDisabled;
        }

        var user = await users.FindByEmailAsync(emailAddress.Trim());
        if (user is null || !user.IsActive || user.Email is null || DemoAccounts.IsDemoEmail(user.Email))
        {
            return Result.Success();
        }

        var token = await users.GeneratePasswordResetTokenAsync(user);
        var link = QueryHelpers.AddQueryString(
            $"{appOptions.Value.PublicUrl!.TrimEnd('/')}/reset-password",
            new Dictionary<string, string?> { ["uid"] = user.Id.ToString(), ["token"] = token });

        await email.SendAsync(new EmailMessage(
            user.Email,
            "Reset your OpenTicket password",
            $"""
            Hi {user.DisplayName},

            Someone asked to reset the password for your OpenTicket account. If it was you, open this link within one hour:

            {link}

            If you did not ask for this, ignore this email. Your password stays the same.
            """), ct);
        LogResetRequested(user.Id);
        return Result.Success();
    }

    public async Task<Result> ResetPasswordAsync(Guid userId, string token, string newPassword, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null || !user.IsActive)
        {
            return AuthErrors.InvalidResetLink;
        }

        var reset = await users.ResetPasswordAsync(user, token, newPassword);
        if (!reset.Succeeded)
        {
            return reset.Errors.Any(e => e.Code == "InvalidToken")
                ? AuthErrors.InvalidResetLink
                : IdentityErrorMapper.ToError(reset, passwordField: "newPassword");
        }

        // Proving control of the mailbox is enough to lift a lockout; old sessions may belong to whoever caused it.
        await users.SetLockoutEndDateAsync(user, null);
        await users.ResetAccessFailedCountAsync(user);
        await auth.RevokeAllSessionsAsync(user.Id, ct);
        LogPasswordReset(user.Id);
        return Result.Success();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Password reset requested for user {UserId}")]
    private partial void LogResetRequested(Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Password reset completed for user {UserId}; all sessions revoked")]
    private partial void LogPasswordReset(Guid userId);
}

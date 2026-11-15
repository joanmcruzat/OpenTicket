using OpenTicket.Domain.Common;

namespace OpenTicket.Application.Auth;

/// <summary>Self-service password reset by email.</summary>
public interface IAccountRecoveryService
{
    bool IsEnabled { get; }

    /// <summary>
    /// Emails a one-hour reset link if an active account has this email. Succeeds either way, so callers cannot
    /// find out which emails are registered.
    /// </summary>
    Task<Result> RequestPasswordResetAsync(string email, CancellationToken ct);

    /// <summary>Sets a new password from a reset link, unlocks the account and signs out every session.</summary>
    Task<Result> ResetPasswordAsync(Guid userId, string token, string newPassword, CancellationToken ct);
}

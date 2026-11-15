using OpenTicket.Application.Common;
using OpenTicket.Domain.Common;

namespace OpenTicket.Application.Auth;

public interface IAuthService
{
    /// <summary>Public sign-up. Always creates a <see cref="Domain.Users.UserRole.Customer"/>.</summary>
    Task<Result<UserProfile>> RegisterAsync(RegisterCommand command, CancellationToken ct);

    Task<Result<AuthSession>> LoginAsync(LoginCommand command, CancellationToken ct);

    /// <summary>Rotates the refresh token. Reuse of a revoked token revokes every session of that user.</summary>
    Task<Result<AuthSession>> RefreshAsync(string refreshToken, CancellationToken ct);

    /// <summary>Revokes the given refresh token if it exists. Always succeeds.</summary>
    Task LogoutAsync(string? refreshToken, CancellationToken ct);

    Task<Result<UserProfile>> GetProfileAsync(Guid userId, CancellationToken ct);

    /// <summary>Changes the caller's password and ends all of their sessions.</summary>
    Task<Result> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken ct);
}

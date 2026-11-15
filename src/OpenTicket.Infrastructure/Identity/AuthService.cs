using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using OpenTicket.Application.Auth;
using OpenTicket.Application.Common;
using OpenTicket.Domain.Common;
using OpenTicket.Domain.Users;
using OpenTicket.Infrastructure.Demo;
using OpenTicket.Infrastructure.Persistence;

namespace OpenTicket.Infrastructure.Identity;

internal sealed partial class AuthService(
    UserManager<ApplicationUser> users,
    OpenTicketDbContext db,
    TokenService tokens,
    IOptions<JwtOptions> jwtOptions,
    TimeProvider clock,
    IOptions<DemoOptions> demo,
    ILogger<AuthService> logger) : IAuthService
{
    public async Task<Result<UserProfile>> RegisterAsync(RegisterCommand command, CancellationToken ct)
    {
        if (demo.Value.Enabled)
        {
            return AuthErrors.RegistrationDisabled;
        }

        var email = command.Email.Trim();
        if (await users.FindByEmailAsync(email) is not null)
        {
            return AuthErrors.EmailTaken;
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            DisplayName = command.DisplayName.Trim(),
            CreatedAtUtc = clock.GetUtcNow().UtcDateTime,
        };

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var created = await users.CreateAsync(user, command.Password);
        if (!created.Succeeded)
        {
            return IdentityErrorMapper.ToError(created);
        }

        var roleAdded = await users.AddToRoleAsync(user, nameof(UserRole.Customer));
        if (!roleAdded.Succeeded)
        {
            throw new InvalidOperationException($"Could not assign the Customer role: {roleAdded}");
        }

        await transaction.CommitAsync(ct);
        LogRegistered(user.Id);
        return ToProfile(user, UserRole.Customer);
    }

    public async Task<Result<AuthSession>> LoginAsync(LoginCommand command, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(command.Email.Trim());
        if (user is null || !user.IsActive)
        {
            return AuthErrors.InvalidCredentials;
        }

        if (await users.IsLockedOutAsync(user))
        {
            LogLockedOutAttempt(user.Id);
            return AuthErrors.InvalidCredentials;
        }

        if (!await users.CheckPasswordAsync(user, command.Password))
        {
            await users.AccessFailedAsync(user);
            LogFailedLogin(user.Id);
            return AuthErrors.InvalidCredentials;
        }

        await users.ResetAccessFailedCountAsync(user);

        var now = clock.GetUtcNow().UtcDateTime;
        await db.RefreshTokens
            .Where(t => t.UserId == user.Id && t.ExpiresAtUtc < now)
            .ExecuteDeleteAsync(ct);

        return await IssueSessionAsync(user, await users.GetRoleAsync(user), ct);
    }

    public async Task<Result<AuthSession>> RefreshAsync(string refreshToken, CancellationToken ct)
    {
        var hash = TokenService.HashRefreshToken(refreshToken);
        var stored = await db.RefreshTokens.AsNoTracking().SingleOrDefaultAsync(t => t.TokenHash == hash, ct);
        var now = clock.GetUtcNow().UtcDateTime;

        if (stored is null || stored.ExpiresAtUtc <= now)
        {
            return AuthErrors.InvalidRefreshToken;
        }

        if (stored.RevokedAtUtc is not null)
        {
            // A rotated-out token was presented again: treat it as stolen and end every session of this user.
            await RevokeAllSessionsAsync(stored.UserId, ct);
            LogRefreshTokenReuse(stored.UserId);
            return AuthErrors.InvalidRefreshToken;
        }

        var user = await users.FindByIdAsync(stored.UserId.ToString());
        if (user is null || !user.IsActive)
        {
            return AuthErrors.InvalidRefreshToken;
        }

        var replacement = NewRefreshToken(user.Id, now);

        // Claim the old token atomically so two concurrent refreshes cannot both rotate it.
        var claimed = await db.RefreshTokens
            .Where(t => t.Id == stored.Id && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.RevokedAtUtc, now)
                .SetProperty(t => t.ReplacedById, replacement.Token.Id), ct);
        if (claimed == 0)
        {
            return AuthErrors.InvalidRefreshToken;
        }

        return await IssueSessionAsync(user, await users.GetRoleAsync(user), replacement, ct);
    }

    public async Task LogoutAsync(string? refreshToken, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(refreshToken))
        {
            return;
        }

        var hash = TokenService.HashRefreshToken(refreshToken);
        var now = clock.GetUtcNow().UtcDateTime;
        await db.RefreshTokens
            .Where(t => t.TokenHash == hash && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, now), ct);
    }

    public async Task<Result<UserProfile>> GetProfileAsync(Guid userId, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        return user is null ? AuthErrors.UserNotFound : ToProfile(user, await users.GetRoleAsync(user));
    }

    public async Task<Result> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return AuthErrors.UserNotFound;
        }

        var changed = await users.ChangePasswordAsync(user, currentPassword, newPassword);
        if (!changed.Succeeded)
        {
            return changed.Errors.Any(e => e.Code == "PasswordMismatch")
                ? Error.Validation("auth.wrong_password", "The current password is incorrect.",
                    new Dictionary<string, string[]> { ["currentPassword"] = ["The current password is incorrect."] })
                : IdentityErrorMapper.ToError(changed, passwordField: "newPassword");
        }

        // Every other device must sign in again with the new password.
        await RevokeAllSessionsAsync(userId, ct);
        return Result.Success();
    }

    /// <summary>Starts a new session for a user who has already been authenticated another way (e.g. GitHub).</summary>
    internal async Task<Result<AuthSession>> StartSessionAsync(ApplicationUser user, CancellationToken ct) =>
        await IssueSessionAsync(user, await users.GetRoleAsync(user), ct);

    internal async Task RevokeAllSessionsAsync(Guid userId, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, now), ct);
    }

    private Task<Result<AuthSession>> IssueSessionAsync(ApplicationUser user, UserRole role, CancellationToken ct) =>
        IssueSessionAsync(user, role, NewRefreshToken(user.Id, clock.GetUtcNow().UtcDateTime), ct);

    private async Task<Result<AuthSession>> IssueSessionAsync(
        ApplicationUser user, UserRole role, (RefreshToken Token, string Raw) refresh, CancellationToken ct)
    {
        db.RefreshTokens.Add(refresh.Token);
        await db.SaveChangesAsync(ct);

        var (accessToken, accessExpires) = tokens.CreateAccessToken(user, role);
        return new AuthSession(accessToken, accessExpires, refresh.Raw, refresh.Token.ExpiresAtUtc, ToProfile(user, role));
    }

    private (RefreshToken Token, string Raw) NewRefreshToken(Guid userId, DateTime now)
    {
        var raw = TokenService.CreateRefreshToken();
        var token = new RefreshToken
        {
            UserId = userId,
            TokenHash = TokenService.HashRefreshToken(raw),
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddDays(jwtOptions.Value.RefreshTokenDays),
        };
        return (token, raw);
    }

    private static UserProfile ToProfile(ApplicationUser user, UserRole role) =>
        new(user.Id, user.Email ?? string.Empty, user.DisplayName, role);

    [LoggerMessage(Level = LogLevel.Information, Message = "User {UserId} registered")]
    private partial void LogRegistered(Guid userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed login for user {UserId}")]
    private partial void LogFailedLogin(Guid userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Login attempt for locked-out user {UserId}")]
    private partial void LogLockedOutAttempt(Guid userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Refresh token reuse detected for user {UserId}; all sessions revoked")]
    private partial void LogRefreshTokenReuse(Guid userId);
}

using OpenTicket.Domain.Users;

namespace OpenTicket.Application.Auth;

public sealed record UserProfile(Guid Id, string Email, string DisplayName, UserRole Role);

/// <summary>Result of a successful login or refresh. The refresh token is delivered as an HttpOnly cookie, never in JSON.</summary>
public sealed record AuthSession(
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc,
    UserProfile User);

public sealed record RegisterCommand(string Email, string Password, string DisplayName);

public sealed record LoginCommand(string Email, string Password);

/// <summary>What a provider (GitHub, Google) told us about the person who just signed in.</summary>
/// <param name="EmailVerified">Only verified emails are used to match or create accounts.</param>
public sealed record ExternalIdentity(
    string Provider, string ProviderKey, string? Email, bool EmailVerified, string? DisplayName);

/// <summary>Sign-in options the login page should offer.</summary>
/// <param name="Demo">Present only when the server runs as a public demo.</param>
public sealed record AuthOptionsResponse(IReadOnlyList<string> ExternalProviders, bool PasswordReset, DemoInfo? Demo);

/// <summary>Public demo settings, when the server runs as a demo.</summary>
/// <param name="ResetIntervalHours">How often demo data is restored; 0 means never.</param>
public sealed record DemoInfo(int ResetIntervalHours);

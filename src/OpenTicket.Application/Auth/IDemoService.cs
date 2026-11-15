using OpenTicket.Domain.Common;
using OpenTicket.Domain.Users;

namespace OpenTicket.Application.Auth;

/// <summary>Public demo mode: one-click sign-in as each role. Every member fails or is null when demo mode is off.</summary>
public interface IDemoService
{
    /// <summary>Null when demo mode is off.</summary>
    DemoInfo? Info { get; }

    /// <summary>Signs in as the demo account for <paramref name="role"/>, no password needed.</summary>
    Task<Result<AuthSession>> SignInAsync(UserRole role, CancellationToken ct);
}

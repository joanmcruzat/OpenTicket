using OpenTicket.Domain.Common;

namespace OpenTicket.Application.Auth;

/// <summary>Signs in with GitHub or Google, creating or linking the OpenTicket account as needed.</summary>
public interface IExternalLoginService
{
    /// <summary>
    /// Finds the account linked to this provider login; otherwise links the account with the same verified email;
    /// otherwise creates a new Reporter account.
    /// </summary>
    Task<Result<AuthSession>> SignInAsync(ExternalIdentity identity, CancellationToken ct);
}

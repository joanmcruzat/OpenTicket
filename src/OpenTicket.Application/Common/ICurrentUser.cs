using OpenTicket.Domain.Users;

namespace OpenTicket.Application.Common;

/// <summary>The authenticated caller of the current request.</summary>
public interface ICurrentUser
{
    /// <summary>The caller. Only call from endpoints that require authentication.</summary>
    Actor Actor { get; }
}

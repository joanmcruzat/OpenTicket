using OpenTicket.Domain.Users;

namespace OpenTicket.Application.Common;

public sealed record UserSummary(Guid Id, string DisplayName, UserRole Role);

/// <summary>Read access to user names and roles, which live in the Identity store.</summary>
public interface IUserDirectory
{
    /// <summary>Looks up many users in one query. Unknown IDs are simply absent from the result.</summary>
    Task<IReadOnlyDictionary<Guid, UserSummary>> GetSummariesAsync(IEnumerable<Guid> userIds, CancellationToken ct);

    Task<bool> IsActiveAgentAsync(Guid userId, CancellationToken ct);
}

public static class UserDirectoryExtensions
{
    public static string? NameOf(this IReadOnlyDictionary<Guid, UserSummary> users, Guid? id) =>
        id is { } value && users.TryGetValue(value, out var user) ? user.DisplayName : null;
}

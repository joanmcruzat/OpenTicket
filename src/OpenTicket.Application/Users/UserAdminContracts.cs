using OpenTicket.Application.Common;
using OpenTicket.Domain.Common;
using OpenTicket.Domain.Users;

namespace OpenTicket.Application.Users;

public sealed record UserListItem(Guid Id, string Email, string DisplayName, UserRole Role, bool IsActive, DateTime CreatedAtUtc);

public sealed record AgentOption(Guid Id, string DisplayName, int OpenTicketCount);

public sealed record UserListQuery(int Page, int PageSize, UserRole? Role, bool? IsActive, string? Search);

public sealed record CreateUserCommand(string Email, string DisplayName, UserRole Role, string TemporaryPassword);

/// <summary>Admin-only user management. Implemented over ASP.NET Core Identity.</summary>
public interface IUserAdminService
{
    Task<PagedResult<UserListItem>> ListAsync(UserListQuery query, CancellationToken ct);

    Task<IReadOnlyList<AgentOption>> ListActiveAgentsAsync(CancellationToken ct);

    Task<Result<UserListItem>> GetAsync(Guid userId, CancellationToken ct);

    Task<Result<UserListItem>> CreateAsync(CreateUserCommand command, CancellationToken ct);

    /// <summary>Admins cannot change their own role; agents must have no open assignments.</summary>
    Task<Result<UserListItem>> ChangeRoleAsync(Guid userId, UserRole role, CancellationToken ct);

    /// <summary>Admins cannot deactivate themselves. Deactivation ends all sessions immediately.</summary>
    Task<Result<UserListItem>> SetActiveAsync(Guid userId, bool isActive, CancellationToken ct);

    Task<Result> ResetPasswordAsync(Guid userId, string temporaryPassword, CancellationToken ct);
}

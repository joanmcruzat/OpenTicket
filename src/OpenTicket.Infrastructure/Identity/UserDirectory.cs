using Microsoft.EntityFrameworkCore;

using OpenTicket.Application.Common;
using OpenTicket.Domain.Users;
using OpenTicket.Infrastructure.Persistence;

namespace OpenTicket.Infrastructure.Identity;

internal sealed class UserDirectory(OpenTicketDbContext db) : IUserDirectory
{
    public async Task<IReadOnlyDictionary<Guid, UserSummary>> GetSummariesAsync(IEnumerable<Guid> userIds, CancellationToken ct)
    {
        var ids = userIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, UserSummary>();
        }

        var rows = await (
                from user in db.Users.AsNoTracking()
                where ids.Contains(user.Id)
                join userRole in db.UserRoles on user.Id equals userRole.UserId
                join role in db.Roles on userRole.RoleId equals role.Id
                select new { user.Id, user.DisplayName, RoleName = role.Name })
            .ToListAsync(ct);

        return rows
            .DistinctBy(r => r.Id)
            .ToDictionary(r => r.Id, r => new UserSummary(r.Id, r.DisplayName, Enum.Parse<UserRole>(r.RoleName!)));
    }

    public Task<bool> IsActiveAgentAsync(Guid userId, CancellationToken ct) => (
            from user in db.Users
            where user.Id == userId && user.IsActive
            join userRole in db.UserRoles on user.Id equals userRole.UserId
            join role in db.Roles on userRole.RoleId equals role.Id
            where role.Name == nameof(UserRole.Agent)
            select user.Id)
        .AnyAsync(ct);
}

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

using OpenTicket.Application.Common;
using OpenTicket.Application.Users;
using OpenTicket.Domain.Common;
using OpenTicket.Domain.Tickets;
using OpenTicket.Domain.Users;
using OpenTicket.Infrastructure.Demo;
using OpenTicket.Infrastructure.Persistence;

namespace OpenTicket.Infrastructure.Identity;

internal sealed class UserAdminService(
    UserManager<ApplicationUser> users,
    OpenTicketDbContext db,
    AuthService auth,
    ICurrentUser currentUser,
    TimeProvider clock) : IUserAdminService
{
    public async Task<PagedResult<UserListItem>> ListAsync(UserListQuery query, CancellationToken ct)
    {
        var rows = UsersWithRoles();
        if (query.Role is { } role)
        {
            var roleName = role.ToString();
            rows = rows.Where(r => r.RoleName == roleName);
        }

        if (query.IsActive is { } isActive)
        {
            rows = rows.Where(r => r.User.IsActive == isActive);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToUpperInvariant();
#pragma warning disable CA1304, CA1311, CA1862 // Translated to SQL upper()/strpos.
            rows = rows.Where(r => r.User.NormalizedEmail!.Contains(term) || r.User.DisplayName.ToUpper().Contains(term));
#pragma warning restore CA1304, CA1311, CA1862
        }

        var page = new PageRequest(query.Page, query.PageSize);
        var total = await rows.CountAsync(ct);
        var items = await rows
            .OrderByDescending(r => r.User.CreatedAtUtc).ThenBy(r => r.User.Id)
            .Skip(page.Skip).Take(page.PageSize)
            .ToListAsync(ct);
        return new PagedResult<UserListItem>(items.Select(ToItem).ToList(), page.Page, page.PageSize, total);
    }

    public async Task<IReadOnlyList<AgentOption>> ListActiveAgentsAsync(CancellationToken ct) =>
        await UsersWithRoles()
            .Where(r => r.RoleName == nameof(UserRole.Agent) && r.User.IsActive)
            .OrderBy(r => r.User.DisplayName)
            .Select(r => new AgentOption(
                r.User.Id,
                r.User.DisplayName,
                db.Tickets.Count(t => t.AssignedToId == r.User.Id && t.Status < TicketStatus.Done)))
            .ToListAsync(ct);

    public async Task<Result<UserListItem>> GetAsync(Guid userId, CancellationToken ct)
    {
        var row = await UsersWithRoles().SingleOrDefaultAsync(r => r.User.Id == userId, ct);
        return row is null ? UserAdminErrors.NotFound : ToItem(row);
    }

    public async Task<Result<UserListItem>> CreateAsync(CreateUserCommand command, CancellationToken ct)
    {
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
        var created = await users.CreateAsync(user, command.TemporaryPassword);
        if (!created.Succeeded)
        {
            return IdentityErrorMapper.ToError(created, passwordField: "temporaryPassword");
        }

        EnsureSucceeded(await users.AddToRoleAsync(user, command.Role.ToString()));
        await transaction.CommitAsync(ct);
        return await GetAsync(user.Id, ct);
    }

    public async Task<Result<UserListItem>> ChangeRoleAsync(Guid userId, UserRole role, CancellationToken ct)
    {
        if (userId == currentUser.Actor.UserId)
        {
            return UserAdminErrors.CannotChangeOwnRole;
        }

        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return UserAdminErrors.NotFound;
        }

        if (DemoAccounts.IsDemoEmail(user.Email))
        {
            return UserAdminErrors.DemoAccountProtected;
        }

        var currentRole = await users.GetRoleAsync(user);
        if (currentRole == role)
        {
            return await GetAsync(userId, ct);
        }

        if (currentRole == UserRole.Agent &&
            await db.Tickets.AnyAsync(t => t.AssignedToId == userId && t.Status < TicketStatus.Done, ct))
        {
            return UserAdminErrors.HasOpenAssignments;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        EnsureSucceeded(await users.RemoveFromRoleAsync(user, currentRole.ToString()));
        EnsureSucceeded(await users.AddToRoleAsync(user, role.ToString()));
        await transaction.CommitAsync(ct);

        // Access tokens carry the role; the per-request check rejects the old one, and refresh tokens are ended here.
        await auth.RevokeAllSessionsAsync(userId, ct);
        return await GetAsync(userId, ct);
    }

    public async Task<Result<UserListItem>> SetActiveAsync(Guid userId, bool isActive, CancellationToken ct)
    {
        if (!isActive && userId == currentUser.Actor.UserId)
        {
            return UserAdminErrors.CannotDeactivateSelf;
        }

        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return UserAdminErrors.NotFound;
        }

        if (DemoAccounts.IsDemoEmail(user.Email))
        {
            return UserAdminErrors.DemoAccountProtected;
        }

        if (user.IsActive != isActive)
        {
            user.IsActive = isActive;
            EnsureSucceeded(await users.UpdateAsync(user));
            if (!isActive)
            {
                await auth.RevokeAllSessionsAsync(userId, ct);
            }
        }

        return await GetAsync(userId, ct);
    }

    public async Task<Result> ResetPasswordAsync(Guid userId, string temporaryPassword, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return UserAdminErrors.NotFound;
        }

        if (DemoAccounts.IsDemoEmail(user.Email))
        {
            return UserAdminErrors.DemoAccountProtected;
        }

        var validation = await ValidatePasswordAsync(user, temporaryPassword);
        if (validation is not null)
        {
            return validation;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        EnsureSucceeded(await users.RemovePasswordAsync(user));
        EnsureSucceeded(await users.AddPasswordAsync(user, temporaryPassword));
        EnsureSucceeded(await users.SetLockoutEndDateAsync(user, null));
        EnsureSucceeded(await users.ResetAccessFailedCountAsync(user));
        await transaction.CommitAsync(ct);
        await auth.RevokeAllSessionsAsync(userId, ct);
        return Result.Success();
    }

    private async Task<Error?> ValidatePasswordAsync(ApplicationUser user, string password)
    {
        foreach (var validator in users.PasswordValidators)
        {
            var result = await validator.ValidateAsync(users, user, password);
            if (!result.Succeeded)
            {
                return IdentityErrorMapper.ToError(result, passwordField: "temporaryPassword");
            }
        }

        return null;
    }

    private IQueryable<UserRow> UsersWithRoles() =>
        from user in db.Users.AsNoTracking()
        join userRole in db.UserRoles on user.Id equals userRole.UserId
        join role in db.Roles on userRole.RoleId equals role.Id
        select new UserRow { User = user, RoleName = role.Name! };

    private static UserListItem ToItem(UserRow row) => new(
        row.User.Id, row.User.Email ?? string.Empty, row.User.DisplayName,
        Enum.Parse<UserRole>(row.RoleName), row.User.IsActive, row.User.CreatedAtUtc);

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Identity operation failed: {result}");
        }
    }

    private sealed class UserRow
    {
        public required ApplicationUser User { get; init; }

        public required string RoleName { get; init; }
    }
}

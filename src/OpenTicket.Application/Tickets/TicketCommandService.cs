using Microsoft.EntityFrameworkCore;

using OpenTicket.Application.Common;
using OpenTicket.Domain.Common;
using OpenTicket.Domain.Tickets;
using OpenTicket.Domain.Users;

namespace OpenTicket.Application.Tickets;

/// <summary>Write side for tickets. The domain decides what is allowed; this class loads, checks visibility and versions, and saves.</summary>
public sealed class TicketCommandService(
    IOpenTicketDbContext db,
    IUserDirectory directory,
    ICurrentUser currentUser,
    TicketQueryService queries,
    TimeProvider clock)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public async Task<Result<TicketDetail>> CreateAsync(CreateTicketCommand command, CancellationToken ct)
    {
        var actor = currentUser.Actor;
        var category = await db.TicketCategories.AsNoTracking().SingleOrDefaultAsync(c => c.Id == command.CategoryId, ct);
        if (category is not { IsActive: true })
        {
            return TicketErrors.CategoryInactive;
        }

        var ticket = Ticket.Create(
            command.Title, command.Description, command.Type, category.Id, command.Priority, actor.UserId, Now);
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync(ct);
        return await queries.ToDetailAsync(ticket, ct);
    }

    public async Task<Result<TicketDetail>> UpdateAsync(UpdateTicketCommand command, CancellationToken ct) =>
        await ExecuteAsync(command.TicketId, command.Version, async (ticket, actor) =>
        {
            Domain.Categories.TicketCategory? category = null;
            if (command.CategoryId is { } categoryId && categoryId != ticket.CategoryId)
            {
                category = await db.TicketCategories.SingleOrDefaultAsync(c => c.Id == categoryId, ct);
                if (category is null)
                {
                    return TicketErrors.CategoryInactive;
                }
            }

            var change = new TicketDetailsChange(command.Title, command.Description, command.Type, command.Priority);
            return ticket.UpdateDetails(actor, change, category, Now);
        }, ct);

    public Task<Result<TicketDetail>> ChangeStatusAsync(ChangeTicketStatusCommand command, CancellationToken ct) =>
        ExecuteAsync(command.TicketId, command.Version,
            (ticket, actor) => Task.FromResult(ticket.ChangeStatus(command.Status, command.Reason, actor, Now)), ct);

    public Task<Result<TicketDetail>> MoveToSprintAsync(MoveTicketToSprintCommand command, CancellationToken ct) =>
        ExecuteAsync(command.TicketId, command.Version, async (ticket, actor) =>
        {
            Domain.Sprints.Sprint? sprint = null;
            if (command.SprintId is { } sprintId)
            {
                sprint = await db.Sprints.SingleOrDefaultAsync(s => s.Id == sprintId, ct);
                if (sprint is null)
                {
                    return Domain.Sprints.SprintErrors.NotFound;
                }
            }

            return ticket.MoveToSprint(sprint, actor, Now);
        }, ct);

    public Task<Result<TicketDetail>> AssignAsync(AssignTicketCommand command, CancellationToken ct) =>
        ExecuteAsync(command.TicketId, command.Version, async (ticket, actor) =>
        {
            // Permission first, so a reporter learns nothing about which user IDs are developers.
            var permission = ticket.EnsureCanAssign(command.AssigneeId, actor);
            if (!permission.IsSuccess)
            {
                return permission;
            }

            return await directory.IsActiveAgentAsync(command.AssigneeId, ct)
                ? ticket.Assign(command.AssigneeId, actor, Now)
                : TicketErrors.AssigneeNotDeveloper;
        }, ct);

    public Task<Result<TicketDetail>> UnassignAsync(Guid ticketId, uint version, CancellationToken ct) =>
        ExecuteAsync(ticketId, version, (ticket, actor) => Task.FromResult(ticket.Unassign(actor, Now)), ct);

    /// <summary>Load → visibility (404) → version (409) → domain operation → save → fresh detail.</summary>
    private async Task<Result<TicketDetail>> ExecuteAsync(
        Guid ticketId, uint version, Func<Ticket, Actor, Task<Result>> operation, CancellationToken ct)
    {
        var actor = currentUser.Actor;
        var ticket = await db.Tickets.SingleOrDefaultAsync(t => t.Id == ticketId, ct);
        if (ticket is null || !ticket.IsVisibleTo(actor))
        {
            return TicketErrors.NotFound;
        }

        if (ticket.Version != version)
        {
            return TicketErrors.StaleVersion;
        }

        var result = await operation(ticket, actor);
        if (!result.IsSuccess)
        {
            return result.Error!;
        }

        // A concurrent writer between load and save surfaces as DbUpdateConcurrencyException → 409.
        await db.SaveChangesAsync(ct);
        return await queries.ToDetailAsync(ticket, ct);
    }
}

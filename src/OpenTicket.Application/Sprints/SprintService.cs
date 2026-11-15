using Microsoft.EntityFrameworkCore;

using OpenTicket.Application.Common;
using OpenTicket.Application.Tickets;
using OpenTicket.Domain.Common;
using OpenTicket.Domain.Sprints;
using OpenTicket.Domain.Tickets;

namespace OpenTicket.Application.Sprints;

/// <summary>Sprint planning for admins and developers: create, start, complete and the planning view.</summary>
public sealed class SprintService(
    IOpenTicketDbContext db,
    ICurrentUser currentUser,
    SprintQueries queries,
    TicketQueryService tickets,
    TimeProvider clock)
{
    private const int CompletedShown = 10;

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    /// <summary>Active first, then planned in creation order, then the most recently completed.</summary>
    public async Task<IReadOnlyList<SprintSummary>> ListAsync(CancellationToken ct)
    {
        var open = await db.Sprints.AsNoTracking()
            .Where(s => s.State != SprintState.Completed)
            .OrderByDescending(s => s.State).ThenBy(s => s.CreatedAtUtc)
            .ToListAsync(ct);
        var completed = await db.Sprints.AsNoTracking()
            .Where(s => s.State == SprintState.Completed)
            .OrderByDescending(s => s.CompletedAtUtc)
            .Take(CompletedShown)
            .ToListAsync(ct);
        return await queries.SummarizeAsync([.. open, .. completed], ct);
    }

    public async Task<Result<SprintPlan>> GetPlanAsync(CancellationToken ct)
    {
        if (!IsStaff)
        {
            return SprintErrors.NotAllowed;
        }

        var open = await db.Sprints.AsNoTracking()
            .Where(s => s.State != SprintState.Completed)
            .OrderByDescending(s => s.State).ThenBy(s => s.CreatedAtUtc)
            .ToListAsync(ct);
        var summaries = await queries.SummarizeAsync(open, ct);

        var sections = new List<SprintPlanSection>();
        foreach (var sprint in summaries)
        {
            var inSprint = db.Tickets.Where(t => t.SprintId == sprint.Id)
                .OrderBy(t => t.Status).ThenByDescending(t => t.Priority).ThenBy(t => t.Number)
                .Take(SprintPlan.SectionLimit);
            sections.Add(new SprintPlanSection(sprint, await tickets.ToSummariesAsync(inSprint, ct)));
        }

        var backlog = db.Tickets.Where(t => t.SprintId == null && t.Status < TicketStatus.Done);
        var backlogItems = await tickets.ToSummariesAsync(
            backlog.OrderByDescending(t => t.Priority).ThenBy(t => t.Number).Take(SprintPlan.SectionLimit), ct);
        return new SprintPlan(sections, new SprintBacklog(await backlog.CountAsync(ct), backlogItems));
    }

    public async Task<Result<SprintSummary>> CreateAsync(CreateSprintCommand command, CancellationToken ct)
    {
        if (!IsStaff)
        {
            return SprintErrors.NotAllowed;
        }

        var created = Sprint.Create(command.Name, command.Goal, currentUser.Actor.UserId, Now);
        if (!created.IsSuccess)
        {
            return created.Error!;
        }

        db.Sprints.Add(created.Value!);
        await db.SaveChangesAsync(ct);
        return (await queries.SummarizeAsync([created.Value!], ct))[0];
    }

    public Task<Result<SprintSummary>> UpdateAsync(UpdateSprintCommand command, CancellationToken ct) =>
        ExecuteAsync(command.SprintId, sprint =>
            Task.FromResult(sprint.Update(command.Name, command.Goal, command.StartDate, command.EndDate)), ct);

    public Task<Result<SprintSummary>> StartAsync(StartSprintCommand command, CancellationToken ct) =>
        ExecuteAsync(command.SprintId, async sprint =>
        {
            // The unique index on the active state is the final guard against two concurrent starts.
            if (await db.Sprints.AnyAsync(s => s.State == SprintState.Active && s.Id != sprint.Id, ct))
            {
                return SprintErrors.AlreadyActive;
            }

            return sprint.Start(command.StartDate, command.EndDate, Now);
        }, ct);

    /// <summary>Completes the active sprint; its unfinished tickets move to a planned sprint or the backlog.</summary>
    public Task<Result<SprintSummary>> CompleteAsync(CompleteSprintCommand command, CancellationToken ct) =>
        ExecuteAsync(command.SprintId, async sprint =>
        {
            Sprint? target = null;
            if (command.MoveOpenTicketsTo is { } targetId)
            {
                target = await db.Sprints.SingleOrDefaultAsync(s => s.Id == targetId, ct);
                if (target is not { State: SprintState.Planned })
                {
                    return SprintErrors.InvalidTarget;
                }
            }

            var completed = sprint.Complete(Now);
            if (!completed.IsSuccess)
            {
                return completed;
            }

            var unfinished = await db.Tickets.Where(t => t.SprintId == sprint.Id && t.Status < TicketStatus.Done).ToListAsync(ct);
            foreach (var ticket in unfinished)
            {
                EnsureMoved(ticket.MoveToSprint(target, currentUser.Actor, Now));
            }

            return Result.Success();
        }, ct);

    /// <summary>Deletes a planned sprint; its tickets go back to the backlog.</summary>
    public async Task<Result> DeleteAsync(Guid sprintId, CancellationToken ct)
    {
        if (!IsStaff)
        {
            return SprintErrors.NotAllowed;
        }

        var sprint = await db.Sprints.SingleOrDefaultAsync(s => s.Id == sprintId, ct);
        if (sprint is null)
        {
            return SprintErrors.NotFound;
        }

        if (sprint.State != SprintState.Planned)
        {
            return SprintErrors.NotPlanned;
        }

        // Includes tickets closed after they were planned, which MoveToSprint would refuse.
        var planned = await db.Tickets.Where(t => t.SprintId == sprint.Id).ToListAsync(ct);
        foreach (var ticket in planned)
        {
            ticket.DetachFromDeletedSprint(currentUser.Actor.UserId, Now);
        }

        db.Sprints.Remove(sprint);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    private bool IsStaff => currentUser.Actor.IsAdmin || currentUser.Actor.IsAgent;

    private async Task<Result<SprintSummary>> ExecuteAsync(Guid sprintId, Func<Sprint, Task<Result>> operation, CancellationToken ct)
    {
        if (!IsStaff)
        {
            return SprintErrors.NotAllowed;
        }

        var sprint = await db.Sprints.SingleOrDefaultAsync(s => s.Id == sprintId, ct);
        if (sprint is null)
        {
            return SprintErrors.NotFound;
        }

        var result = await operation(sprint);
        if (!result.IsSuccess)
        {
            return result.Error!;
        }

        await db.SaveChangesAsync(ct);
        return (await queries.SummarizeAsync([sprint], ct))[0];
    }

    /// <summary>Only unfinished (open) tickets are moved and staff may always plan, so a failure is a bug.</summary>
    private static void EnsureMoved(Result result)
    {
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException($"Moving a ticket out of a sprint failed: {result.Error!.Message}");
        }
    }
}

using OpenTicket.Domain.Common;
using OpenTicket.Domain.Users;

namespace OpenTicket.Domain.Tickets;

/// <summary>Status transitions and assignment (docs/architecture.md §11).</summary>
public sealed partial class Ticket
{
    private static readonly TicketStatus[] AllStatuses = Enum.GetValues<TicketStatus>();

    /// <summary>
    /// The statuses <paramref name="actor"/> may move this ticket to right now. Clients use it to offer only valid
    /// moves; <see cref="ChangeStatus"/> applies the same rules.
    /// </summary>
    public IReadOnlyList<TicketStatus> AllowedStatuses(Actor actor) =>
        [.. AllStatuses.Where(target => target != Status && CheckTransition(target, actor) is null)];

    /// <summary>
    /// Open statuses (Backlog, To Do, In Progress, In Review) move freely between each other and to Done or Won't Do.
    /// Done and Won't Do can only be reopened to To Do. Won't Do needs a reason.
    /// Admins move any ticket; developers the tickets assigned to them; reporters can only drop their own ticket
    /// before work starts, or reopen it.
    /// </summary>
    public Result ChangeStatus(TicketStatus target, string? reason, Actor actor, DateTime nowUtc)
    {
        if (target == Status)
        {
            return Result.Success();
        }

        if (CheckTransition(target, actor) is { } error)
        {
            return error;
        }

        string? historyValue = null;
        switch (target)
        {
            case TicketStatus.WontDo:
                if (string.IsNullOrWhiteSpace(reason))
                {
                    return TicketErrors.ReasonRequired;
                }

                var note = reason.Trim();
                ArgumentOutOfRangeException.ThrowIfGreaterThan(note.Length, ClosureNoteMaxLength, nameof(reason));
                ClosureNote = note;
                ClosedAtUtc = nowUtc;
                historyValue = $"{TicketStatus.WontDo}: {note}";
                break;
            case TicketStatus.Done:
                ClosedAtUtc = nowUtc;
                break;
            default:
                // Reopening clears the previous outcome.
                ClosedAtUtc = null;
                ClosureNote = null;
                break;
        }

        Record(actor.UserId, TicketChangeType.StatusChanged, Status.ToString(), historyValue ?? target.ToString(), nowUtc);
        Status = target;
        UpdatedAtUtc = nowUtc;
        return Result.Success();
    }

    /// <summary>
    /// Admins assign any open ticket to an active developer (the caller verifies that). Developers can only take an
    /// unassigned ticket for themselves ("Assign to me").
    /// </summary>
    public Result Assign(Guid assigneeId, Actor actor, DateTime nowUtc)
    {
        var check = EnsureCanAssign(assigneeId, actor);
        if (!check.IsSuccess || AssignedToId == assigneeId)
        {
            return check;
        }

        Record(actor.UserId, TicketChangeType.Assigned, AssignedToId?.ToString(), assigneeId.ToString(), nowUtc);
        AssignedToId = assigneeId;
        UpdatedAtUtc = nowUtc;
        return Result.Success();
    }

    /// <summary>The permission part of <see cref="Assign"/>, without changing anything.</summary>
    public Result EnsureCanAssign(Guid assigneeId, Actor actor)
    {
        if (!actor.IsAdmin && !(actor.IsAgent && assigneeId == actor.UserId))
        {
            return TicketErrors.NotAllowed;
        }

        if (IsClosed)
        {
            return TicketErrors.Closed;
        }

        return actor.IsAdmin || AssignedToId is null || AssignedToId == assigneeId
            ? Result.Success()
            : TicketErrors.AlreadyAssigned;
    }

    /// <summary>
    /// Plans the ticket into <paramref name="sprint"/>, or back to the backlog when null. Admins and developers
    /// plan; reporters cannot. The status is left alone: status changes go through <see cref="ChangeStatus"/> only.
    /// </summary>
    public Result MoveToSprint(Sprints.Sprint? sprint, Actor actor, DateTime nowUtc)
    {
        if (!actor.IsAdmin && !actor.IsAgent)
        {
            return TicketErrors.NotAllowed;
        }

        if (IsClosed)
        {
            return TicketErrors.Closed;
        }

        if (sprint is { IsOpen: false })
        {
            return TicketErrors.SprintClosed;
        }

        if (SprintId == sprint?.Id)
        {
            return Result.Success();
        }

        Record(actor.UserId, TicketChangeType.SprintChanged, SprintId?.ToString(), sprint?.Id.ToString(), nowUtc);
        SprintId = sprint?.Id;
        UpdatedAtUtc = nowUtc;
        return Result.Success();
    }

    /// <summary>
    /// Releases the ticket because its sprint is being deleted. Unlike <see cref="MoveToSprint"/> this also applies
    /// to closed tickets: a deleted sprint must not keep any ticket, and the change is still recorded in history.
    /// The caller has already authorized deleting the sprint.
    /// </summary>
    public void DetachFromDeletedSprint(Guid actorId, DateTime nowUtc)
    {
        if (SprintId is null)
        {
            return;
        }

        Record(actorId, TicketChangeType.SprintChanged, SprintId.ToString(), null, nowUtc);
        SprintId = null;
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>Admins unassign anyone, developers themselves; not while the work is In Progress or In Review.</summary>
    public Result Unassign(Actor actor, DateTime nowUtc)
    {
        if (!actor.IsAdmin && !IsAssignee(actor))
        {
            return TicketErrors.NotAllowed;
        }

        if (IsClosed)
        {
            return TicketErrors.Closed;
        }

        if (AssignedToId is null)
        {
            return Result.Success();
        }

        if (Status is TicketStatus.InProgress or TicketStatus.InReview)
        {
            return TicketErrors.UnassignWhileInProgress;
        }

        Record(actor.UserId, TicketChangeType.Unassigned, AssignedToId.ToString(), null, nowUtc);
        AssignedToId = null;
        UpdatedAtUtc = nowUtc;
        return Result.Success();
    }

    private Error? CheckTransition(TicketStatus target, Actor actor)
    {
        var reopening = IsClosed;
        if (reopening && target != TicketStatus.ToDo)
        {
            return TicketErrors.ReopenToToDoOnly;
        }

        var allowed = actor.IsAdmin
            || IsAssignee(actor)
            || (IsOwner(actor) && (reopening || (target == TicketStatus.WontDo && !IsStarted)));
        if (!allowed)
        {
            return TicketErrors.NotAllowed;
        }

        return target is TicketStatus.InProgress or TicketStatus.InReview && AssignedToId is null
            ? TicketErrors.NotAssigned
            : null;
    }
}

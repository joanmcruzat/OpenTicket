using OpenTicket.Application.Tickets;
using OpenTicket.Domain.Sprints;

namespace OpenTicket.Application.Sprints;

/// <param name="TicketCount">Every ticket in the sprint.</param>
/// <param name="DoneCount">Tickets in Done or Won't Do.</param>
public sealed record SprintSummary(
    Guid Id,
    string Name,
    string? Goal,
    SprintState State,
    DateOnly? StartDate,
    DateOnly? EndDate,
    DateTime? StartedAtUtc,
    DateTime? CompletedAtUtc,
    int TicketCount,
    int DoneCount);

public sealed record SprintPlanSection(SprintSummary Sprint, IReadOnlyList<TicketSummary> Tickets);

/// <param name="Total">All open tickets without a sprint; <paramref name="Tickets"/> may hold fewer.</param>
public sealed record SprintBacklog(int Total, IReadOnlyList<TicketSummary> Tickets);

/// <summary>The planning view: the active sprint, then planned sprints, then the unplanned backlog.</summary>
public sealed record SprintPlan(IReadOnlyList<SprintPlanSection> Sprints, SprintBacklog Backlog)
{
    public const int SectionLimit = 200;
}

public sealed record CreateSprintCommand(string Name, string? Goal);

public sealed record UpdateSprintCommand(Guid SprintId, string Name, string? Goal, DateOnly? StartDate, DateOnly? EndDate);

public sealed record StartSprintCommand(Guid SprintId, DateOnly StartDate, DateOnly EndDate);

/// <param name="MoveOpenTicketsTo">A planned sprint for unfinished tickets, or null for the backlog.</param>
public sealed record CompleteSprintCommand(Guid SprintId, Guid? MoveOpenTicketsTo);

using OpenTicket.Domain.Common;

namespace OpenTicket.Domain.Sprints;

/// <summary>
/// A time-boxed set of tickets: Planned → Active → Completed. At most one sprint is active at a time
/// (enforced by a unique index). Tickets point at their sprint; see <see cref="Tickets.Ticket.MoveToSprint"/>.
/// </summary>
public sealed class Sprint
{
    public const int NameMaxLength = 100;
    public const int GoalMaxLength = 500;

    private Sprint()
    {
        Name = string.Empty;
    }

    private Sprint(string name, string? goal, Guid createdById, DateTime nowUtc)
    {
        Id = Guid.CreateVersion7();
        Name = name;
        Goal = goal;
        State = SprintState.Planned;
        CreatedById = createdById;
        CreatedAtUtc = nowUtc;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; }

    public string? Goal { get; private set; }

    public SprintState State { get; private set; }

    /// <summary>Planned dates; required once the sprint starts.</summary>
    public DateOnly? StartDate { get; private set; }

    public DateOnly? EndDate { get; private set; }

    public DateTime? StartedAtUtc { get; private set; }

    public DateTime? CompletedAtUtc { get; private set; }

    public Guid CreatedById { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public bool IsOpen => State != SprintState.Completed;

    public static Result<Sprint> Create(string name, string? goal, Guid createdById, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return SprintErrors.Blank("name");
        }

        ArgumentOutOfRangeException.ThrowIfGreaterThan(name.Trim().Length, NameMaxLength, nameof(name));
        return new Sprint(name.Trim(), NormalizeGoal(goal), createdById, nowUtc);
    }

    /// <summary>Renames, re-states the goal, or re-plans the dates of a planned or active sprint.</summary>
    public Result Update(string name, string? goal, DateOnly? startDate, DateOnly? endDate)
    {
        if (State == SprintState.Completed)
        {
            return SprintErrors.Completed;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return SprintErrors.Blank("name");
        }

        if (startDate is { } start && endDate is { } end && end < start)
        {
            return SprintErrors.InvalidDates;
        }

        if (State == SprintState.Active && (startDate is null || endDate is null))
        {
            return SprintErrors.InvalidDates;
        }

        ArgumentOutOfRangeException.ThrowIfGreaterThan(name.Trim().Length, NameMaxLength, nameof(name));
        Name = name.Trim();
        Goal = NormalizeGoal(goal);
        StartDate = startDate;
        EndDate = endDate;
        return Result.Success();
    }

    /// <summary>The caller checks that no other sprint is active; the database enforces it too.</summary>
    public Result Start(DateOnly startDate, DateOnly endDate, DateTime nowUtc)
    {
        if (State != SprintState.Planned)
        {
            return SprintErrors.NotPlanned;
        }

        if (endDate < startDate)
        {
            return SprintErrors.InvalidDates;
        }

        StartDate = startDate;
        EndDate = endDate;
        State = SprintState.Active;
        StartedAtUtc = nowUtc;
        return Result.Success();
    }

    /// <summary>Marks the sprint completed. Moving its unfinished tickets is the caller's job (it owns the tickets).</summary>
    public Result Complete(DateTime nowUtc)
    {
        if (State != SprintState.Active)
        {
            return SprintErrors.NotActive;
        }

        State = SprintState.Completed;
        CompletedAtUtc = nowUtc;
        return Result.Success();
    }

    private static string? NormalizeGoal(string? goal)
    {
        if (string.IsNullOrWhiteSpace(goal))
        {
            return null;
        }

        var trimmed = goal.Trim();
        ArgumentOutOfRangeException.ThrowIfGreaterThan(trimmed.Length, GoalMaxLength, nameof(goal));
        return trimmed;
    }
}

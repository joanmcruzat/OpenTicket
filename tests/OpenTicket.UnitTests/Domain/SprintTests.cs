using OpenTicket.Domain.Sprints;
using OpenTicket.Domain.Tickets;
using OpenTicket.UnitTests.Builders;

using static OpenTicket.UnitTests.Builders.TicketScenario;
using static OpenTicket.UnitTests.Domain.TicketWorkflowTests;

namespace OpenTicket.UnitTests.Domain;

public sealed class SprintTests
{
    private static readonly DateOnly Day = new(2026, 11, 9);

    private static Sprint NewSprint() => Sprint.Create("Sprint 1", "  Goal  ", Guid.CreateVersion7(), Now).Value!;

    [Fact]
    public void Create_TrimsAndStartsPlanned_BlankNameIsRejected()
    {
        var sprint = NewSprint();

        Assert.Equal(SprintState.Planned, sprint.State);
        Assert.Equal("Goal", sprint.Goal);
        Assert.Equal("sprint.field_blank", Sprint.Create("  ", null, Guid.CreateVersion7(), Now).Error?.Code);
    }

    [Fact]
    public void Start_SetsDatesAndActive_OnlyFromPlanned_AndEndNotBeforeStart()
    {
        var sprint = NewSprint();

        AssertError(sprint.Start(Day, Day.AddDays(-1), Now), "sprint.invalid_dates");
        Assert.True(sprint.Start(Day, Day.AddDays(14), Now).IsSuccess);
        Assert.Equal(SprintState.Active, sprint.State);
        Assert.Equal(Now, sprint.StartedAtUtc);
        AssertError(sprint.Start(Day, Day.AddDays(14), Now), "sprint.not_planned");
    }

    [Fact]
    public void Complete_OnlyTheActiveSprint_ThenItIsFrozen()
    {
        var sprint = NewSprint();
        AssertError(sprint.Complete(Now), "sprint.not_active");
        sprint.Start(Day, Day.AddDays(14), Now);

        Assert.True(sprint.Complete(Now.AddDays(14)).IsSuccess);

        Assert.False(sprint.IsOpen);
        AssertError(sprint.Update("Renamed", null, Day, Day), "sprint.completed");
    }

    [Fact]
    public void MoveToSprint_KeepsTheStatus_AndRecordsOnlyTheSprintChange()
    {
        // Status changes go through ChangeStatus only (CLAUDE.md); planning never moves a ticket's status.
        var s = Backlog();
        var sprint = NewSprint();

        Assert.True(s.Ticket.MoveToSprint(sprint, s.Agent, Now.AddHours(1)).IsSuccess);

        Assert.Equal(sprint.Id, s.Ticket.SprintId);
        Assert.Equal(TicketStatus.Backlog, s.Ticket.Status);
        var entry = Assert.Single(s.Ticket.History.Skip(1));
        Assert.Equal(TicketChangeType.SprintChanged, entry.ChangeType);
        Assert.Equal(sprint.Id.ToString(), entry.NewValue);
    }

    [Fact]
    public void DetachFromDeletedSprint_WorksOnClosedTickets_AndRecordsHistory()
    {
        var s = InProgress();
        var sprint = NewSprint();
        s.Ticket.MoveToSprint(sprint, s.Admin, Now);
        s.Ticket.ChangeStatus(TicketStatus.Done, null, s.Admin, Now);

        s.Ticket.DetachFromDeletedSprint(s.Admin.UserId, Now.AddHours(1));

        Assert.Null(s.Ticket.SprintId);
        var entry = s.Ticket.History.Last();
        Assert.Equal(TicketChangeType.SprintChanged, entry.ChangeType);
        Assert.Equal(sprint.Id.ToString(), entry.OldValue);
        Assert.Null(entry.NewValue);
    }

    [Fact]
    public void DetachFromDeletedSprint_WithoutSprint_RecordsNothing()
    {
        var s = Backlog();
        var before = s.Ticket.History.Count;

        s.Ticket.DetachFromDeletedSprint(s.Admin.UserId, Now);

        Assert.Equal(before, s.Ticket.History.Count);
    }

    [Fact]
    public void MoveToSprint_InProgressTicketKeepsItsStatus_AndCanReturnToBacklog()
    {
        var s = InProgress();
        var sprint = NewSprint();

        s.Ticket.MoveToSprint(sprint, s.Admin, Now);
        Assert.Equal(TicketStatus.InProgress, s.Ticket.Status);
        Assert.True(s.Ticket.MoveToSprint(null, s.Admin, Now).IsSuccess);
        Assert.Null(s.Ticket.SprintId);
    }

    [Fact]
    public void MoveToSprint_ByReporter_ClosedTicket_OrCompletedSprint_IsRejected()
    {
        var s = Backlog();
        var completed = NewSprint();
        completed.Start(Day, Day.AddDays(1), Now);
        completed.Complete(Now);

        AssertError(s.Ticket.MoveToSprint(NewSprint(), s.Owner, Now), "ticket.forbidden");
        AssertError(s.Ticket.MoveToSprint(completed, s.Admin, Now), "ticket.sprint_closed");
        var done = Done();
        AssertError(done.Ticket.MoveToSprint(NewSprint(), done.Admin, Now), "ticket.closed");
    }
}

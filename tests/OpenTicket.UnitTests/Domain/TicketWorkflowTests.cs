using OpenTicket.Domain.Common;
using OpenTicket.Domain.Tickets;
using OpenTicket.UnitTests.Builders;

using static OpenTicket.UnitTests.Builders.TicketScenario;

namespace OpenTicket.UnitTests.Domain;

public sealed class TicketWorkflowTests
{
    [Theory]
    [InlineData("Admin", true)]
    [InlineData("Owner", true)]
    [InlineData("Agent", true)]
    [InlineData("OtherAgent", true)]
    [InlineData("OtherCustomer", false)]
    public void IsVisibleTo_StaffSeeEverything_ReportersOnlyTheirOwn(string actor, bool expected)
    {
        var s = Backlog();

        Assert.Equal(expected, s.Ticket.IsVisibleTo(s.ActorFor(actor)));
    }

    [Theory]
    [InlineData(TicketStatus.Backlog, TicketStatus.InReview)]
    [InlineData(TicketStatus.ToDo, TicketStatus.Done)]
    [InlineData(TicketStatus.InProgress, TicketStatus.Backlog)]
    [InlineData(TicketStatus.InReview, TicketStatus.InProgress)]
    [InlineData(TicketStatus.Backlog, TicketStatus.Done)]
    public void ChangeStatus_AssigneeBetweenOpenStatuses_MovesFreelyAndRecordsHistory(TicketStatus from, TicketStatus to)
    {
        var s = InStatus(from);
        var historyBefore = s.Ticket.History.Count;

        var result = s.Ticket.ChangeStatus(to, null, s.Agent, Now.AddHours(1));

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(to, s.Ticket.Status);
        Assert.Equal(Now.AddHours(1), s.Ticket.UpdatedAtUtc);
        var entry = s.Ticket.History.Last();
        Assert.Equal(historyBefore + 1, s.Ticket.History.Count);
        Assert.Equal(TicketChangeType.StatusChanged, entry.ChangeType);
        Assert.Equal(from.ToString(), entry.OldValue);
        Assert.Equal(to.ToString(), entry.NewValue);
    }

    [Fact]
    public void ChangeStatus_ToDone_SetsClosedAt()
    {
        var s = InProgress();

        Assert.True(s.Ticket.ChangeStatus(TicketStatus.Done, null, s.Agent, Now.AddDays(1)).IsSuccess);

        Assert.True(s.Ticket.IsClosed);
        Assert.Equal(Now.AddDays(1), s.Ticket.ClosedAtUtc);
        Assert.Null(s.Ticket.ClosureNote);
    }

    [Fact]
    public void ChangeStatus_SameStatus_IsNoOp()
    {
        var s = InProgress();
        var historyBefore = s.Ticket.History.Count;

        Assert.True(s.Ticket.ChangeStatus(TicketStatus.InProgress, null, s.Agent, Now.AddDays(1)).IsSuccess);

        Assert.Equal(historyBefore, s.Ticket.History.Count);
    }

    [Theory]
    [InlineData("OtherAgent")]
    [InlineData("Owner")]
    [InlineData("OtherCustomer")]
    public void ChangeStatus_NotAdminOrAssignee_IsForbidden(string actor)
    {
        var s = Assigned();

        AssertError(s.Ticket.ChangeStatus(TicketStatus.ToDo, null, s.ActorFor(actor), Now), "ticket.forbidden");
    }

    [Fact]
    public void ChangeStatus_AdminOnAnyTicket_IsAllowed()
    {
        var s = Backlog();

        Assert.True(s.Ticket.ChangeStatus(TicketStatus.ToDo, null, s.Admin, Now).IsSuccess);
        Assert.True(s.Ticket.ChangeStatus(TicketStatus.Done, null, s.Admin, Now).IsSuccess);
    }

    [Theory]
    [InlineData(TicketStatus.InProgress)]
    [InlineData(TicketStatus.InReview)]
    public void ChangeStatus_ToActiveWorkWithoutAssignee_RequiresAssignment(TicketStatus target)
    {
        var s = Backlog();

        AssertError(s.Ticket.ChangeStatus(target, null, s.Admin, Now), "ticket.not_assigned");
    }

    [Fact]
    public void ChangeStatus_ToWontDoWithReason_ClosesWithTrimmedNote()
    {
        var s = Assigned();

        var result = s.Ticket.ChangeStatus(TicketStatus.WontDo, "  Duplicate of OT-7  ", s.Agent, Now.AddHours(2));

        Assert.True(result.IsSuccess);
        Assert.Equal(TicketStatus.WontDo, s.Ticket.Status);
        Assert.Equal("Duplicate of OT-7", s.Ticket.ClosureNote);
        Assert.Equal(Now.AddHours(2), s.Ticket.ClosedAtUtc);
        Assert.Equal("WontDo: Duplicate of OT-7", s.Ticket.History.Last().NewValue);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ChangeStatus_ToWontDoWithoutReason_IsValidationError(string? reason)
    {
        var s = Assigned();

        AssertError(s.Ticket.ChangeStatus(TicketStatus.WontDo, reason, s.Agent, Now), "ticket.reason_required");
        Assert.Equal(TicketStatus.Backlog, s.Ticket.Status);
    }

    [Theory]
    [InlineData(TicketStatus.Backlog, true)]
    [InlineData(TicketStatus.ToDo, true)]
    [InlineData(TicketStatus.InProgress, false)]
    public void ChangeStatus_ReporterDroppingOwnTicket_OnlyBeforeWorkStarts(TicketStatus from, bool allowed)
    {
        var s = InStatus(from);

        var result = s.Ticket.ChangeStatus(TicketStatus.WontDo, "No longer needed", s.Owner, Now);

        Assert.Equal(allowed, result.IsSuccess);
    }

    [Fact]
    public void ChangeStatus_ReporterMovingToOtherOpenStatus_IsForbidden()
    {
        var s = Backlog();

        AssertError(s.Ticket.ChangeStatus(TicketStatus.ToDo, null, s.Owner, Now), "ticket.forbidden");
    }

    [Theory]
    [InlineData(TicketStatus.Done)]
    [InlineData(TicketStatus.WontDo)]
    public void Reopen_ClosedTicketToToDo_ClearsOutcome(TicketStatus closed)
    {
        var s = InStatus(closed);

        Assert.True(s.Ticket.ChangeStatus(TicketStatus.ToDo, null, s.Owner, Now.AddDays(3)).IsSuccess);

        Assert.Equal(TicketStatus.ToDo, s.Ticket.Status);
        Assert.Null(s.Ticket.ClosedAtUtc);
        Assert.Null(s.Ticket.ClosureNote);
    }

    [Theory]
    [InlineData(TicketStatus.Backlog)]
    [InlineData(TicketStatus.InProgress)]
    [InlineData(TicketStatus.WontDo)]
    public void Reopen_ClosedTicketToAnythingButToDo_IsRejected(TicketStatus target)
    {
        var s = Done();

        AssertError(s.Ticket.ChangeStatus(target, "x", s.Admin, Now), "ticket.invalid_transition");
    }

    [Fact]
    public void AllowedStatuses_Assignee_OffersEveryOtherStatus()
    {
        var s = InProgress();

        Assert.Equal(
            [TicketStatus.Backlog, TicketStatus.ToDo, TicketStatus.InReview, TicketStatus.Done, TicketStatus.WontDo],
            s.Ticket.AllowedStatuses(s.Agent));
    }

    [Fact]
    public void AllowedStatuses_UnassignedTicket_SkipsActiveWorkStatuses()
    {
        var s = Backlog();

        Assert.Equal([TicketStatus.ToDo, TicketStatus.Done, TicketStatus.WontDo], s.Ticket.AllowedStatuses(s.Admin));
    }

    [Fact]
    public void AllowedStatuses_ClosedTicket_OnlyReopen_AndReporterOnlyDropWhileNotStarted()
    {
        var done = Done();
        Assert.Equal([TicketStatus.ToDo], done.Ticket.AllowedStatuses(done.Owner));
        var s = Backlog();
        Assert.Equal([TicketStatus.WontDo], s.Ticket.AllowedStatuses(s.Owner));
        Assert.Empty(s.Ticket.AllowedStatuses(s.OtherAgent));
    }

    [Fact]
    public void Assign_ByAdmin_SetsAssigneeAndRecordsHistory()
    {
        var s = Backlog();

        Assert.True(s.Ticket.Assign(s.Agent.UserId, s.Admin, Now).IsSuccess);

        Assert.Equal(s.Agent.UserId, s.Ticket.AssignedToId);
        Assert.Equal(TicketChangeType.Assigned, s.Ticket.History.Last().ChangeType);
    }

    [Fact]
    public void Assign_AdminReassigning_IsAllowed()
    {
        var s = InProgress();

        Assert.True(s.Ticket.Assign(s.OtherAgent.UserId, s.Admin, Now).IsSuccess);
        Assert.Equal(s.OtherAgent.UserId, s.Ticket.AssignedToId);
    }

    [Fact]
    public void Assign_DeveloperTakingUnassignedTicket_AssignsThemself()
    {
        var s = Backlog();

        Assert.True(s.Ticket.Assign(s.OtherAgent.UserId, s.OtherAgent, Now).IsSuccess);
        Assert.Equal(s.OtherAgent.UserId, s.Ticket.AssignedToId);
    }

    [Fact]
    public void Assign_DeveloperTakingSomeoneElsesTicket_IsRejected()
    {
        var s = Assigned();

        AssertError(s.Ticket.Assign(s.OtherAgent.UserId, s.OtherAgent, Now), "ticket.already_assigned");
    }

    [Theory]
    [InlineData("Agent")]
    [InlineData("Owner")]
    public void Assign_NonAdminAssigningSomeoneElse_IsForbidden(string actor)
    {
        var s = Backlog();

        AssertError(s.Ticket.Assign(s.OtherAgent.UserId, s.ActorFor(actor), Now), "ticket.forbidden");
    }

    [Fact]
    public void Assign_ClosedTicket_IsRejected()
    {
        var s = Done();

        AssertError(s.Ticket.Assign(s.OtherAgent.UserId, s.Admin, Now), "ticket.closed");
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Agent")]
    public void Unassign_AdminOrSelfBeforeWorkStarts_ClearsAssignee(string actor)
    {
        var s = InStatus(TicketStatus.ToDo);

        Assert.True(s.Ticket.Unassign(s.ActorFor(actor), Now).IsSuccess);
        Assert.Null(s.Ticket.AssignedToId);
    }

    [Fact]
    public void Unassign_WhileInProgress_IsRejected()
    {
        var s = InProgress();

        AssertError(s.Ticket.Unassign(s.Admin, Now), "ticket.unassign_in_progress");
    }

    [Fact]
    public void Unassign_OtherDeveloper_IsForbidden()
    {
        var s = Assigned();

        AssertError(s.Ticket.Unassign(s.OtherAgent, Now), "ticket.forbidden");
    }

    internal static void AssertError(Result result, string expectedCode)
    {
        Assert.False(result.IsSuccess, $"Expected {expectedCode} but the operation succeeded.");
        Assert.Equal(expectedCode, result.Error!.Code);
    }
}

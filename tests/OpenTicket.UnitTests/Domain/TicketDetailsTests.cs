using OpenTicket.Domain.Categories;
using OpenTicket.Domain.Common;
using OpenTicket.Domain.Tickets;
using OpenTicket.Domain.Users;
using OpenTicket.UnitTests.Builders;

using static OpenTicket.UnitTests.Builders.TicketScenario;
using static OpenTicket.UnitTests.Domain.TicketWorkflowTests;

namespace OpenTicket.UnitTests.Domain;

public sealed class TicketDetailsTests
{
    [Fact]
    public void UpdateDetails_OwnerInBacklog_ChangesFieldsAndRecordsEachChange()
    {
        var s = Backlog();
        var newCategory = new TicketCategory("Frontend", null, Now);

        var result = s.Ticket.UpdateDetails(
            s.Owner, new(" New title ", "New description", TicketType.Story, TicketPriority.Urgent), newCategory, Now.AddMinutes(5));

        Assert.True(result.IsSuccess);
        Assert.Equal("New title", s.Ticket.Title);
        Assert.Equal(TicketType.Story, s.Ticket.Type);
        Assert.Equal(TicketPriority.Urgent, s.Ticket.Priority);
        Assert.Equal(newCategory.Id, s.Ticket.CategoryId);
        Assert.Equal(Now.AddMinutes(5), s.Ticket.UpdatedAtUtc);
        var types = s.Ticket.History.Skip(1).Select(h => h.ChangeType).ToList();
        Assert.Equal(
            [
                TicketChangeType.DetailsEdited, TicketChangeType.DetailsEdited, TicketChangeType.TypeChanged,
                TicketChangeType.PriorityChanged, TicketChangeType.CategoryChanged,
            ],
            types);
    }

    [Fact]
    public void UpdateDetails_SameValues_RecordsNothing()
    {
        var s = Backlog();

        var result = s.Ticket.UpdateDetails(
            s.Owner, new("Login button unresponsive", null, TicketType.Bug, TicketPriority.Medium), s.Category, Now.AddDays(1));

        Assert.True(result.IsSuccess);
        Assert.Single(s.Ticket.History);
        Assert.Equal(Now, s.Ticket.UpdatedAtUtc);
    }

    [Fact]
    public void UpdateDetails_OwnerAfterWorkStarted_IsNotEditable()
    {
        var s = InProgress();

        AssertError(Edit(s, s.Owner, "Changed"), "ticket.not_editable");
    }

    [Fact]
    public void UpdateDetails_AssignedDeveloper_MayEditEverything()
    {
        var s = InProgress();

        Assert.True(s.Ticket.UpdateDetails(s.Agent, new("Clearer title", "Steps to reproduce", TicketType.Task, TicketPriority.High), null, Now).IsSuccess);
        Assert.Equal("Clearer title", s.Ticket.Title);
    }

    [Theory]
    [InlineData("OtherAgent")]
    [InlineData("OtherCustomer")]
    public void UpdateDetails_NotAdminAssigneeOrOwner_IsForbidden(string actor)
    {
        var s = Assigned();

        AssertError(Edit(s, s.ActorFor(actor), "Hijack"), "ticket.forbidden");
    }

    [Fact]
    public void UpdateDetails_ClosedTicket_IsRejected()
    {
        var s = Done();

        AssertError(Edit(s, s.Admin, "Late edit"), "ticket.closed");
    }

    [Fact]
    public void UpdateDetails_InactiveCategory_IsValidationError()
    {
        var s = Backlog();
        var inactive = new TicketCategory("Retired", null, Now);
        inactive.Deactivate(Now);

        AssertError(s.Ticket.UpdateDetails(s.Owner, new(null, null, null, null), inactive, Now), "ticket.category_invalid");
    }

    [Fact]
    public void UpdateDetails_BlankTitle_IsValidationError()
    {
        var s = Backlog();

        AssertError(Edit(s, s.Owner, "   "), "ticket.field_blank");
    }

    [Fact]
    public void UpdateDetails_AdminWhileInReview_MayEditText()
    {
        var s = InStatus(TicketStatus.InReview);

        Assert.True(Edit(s, s.Admin, "Clarified title").IsSuccess);
    }

    [Fact]
    public void CanEdit_MatchesUpdateRules()
    {
        var s = InProgress();

        Assert.True(s.Ticket.CanEdit(s.Admin));
        Assert.True(s.Ticket.CanEdit(s.Agent));
        Assert.False(s.Ticket.CanEdit(s.Owner));
        Assert.False(s.Ticket.CanEdit(s.OtherAgent));
    }

    [Fact]
    public void EnsureCanComment_VisibleOpenTicket_Succeeds_InvisibleIsNotFound_ClosedIsRejected()
    {
        var s = Assigned();

        Assert.True(s.Ticket.EnsureCanComment(s.OtherAgent).IsSuccess);
        AssertError(s.Ticket.EnsureCanComment(s.OtherCustomer), "ticket.not_found");
        var done = Done();
        AssertError(done.Ticket.EnsureCanComment(done.Owner), "ticket.closed");
    }

    private static Result Edit(TicketScenario s, Actor actor, string title) =>
        s.Ticket.UpdateDetails(actor, new(title, null, null, null), null, Now);
}

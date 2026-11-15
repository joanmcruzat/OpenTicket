using OpenTicket.Domain.Tickets;

namespace OpenTicket.UnitTests.Domain;

public sealed class TicketTests
{
    private static readonly DateTime Now = new(2026, 11, 7, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_ValidInput_StartsInBacklogWithTrimmedTextAndCreatedHistory()
    {
        var reporterId = Guid.CreateVersion7();

        var ticket = Ticket.Create("  Login fails ", " Error 500 on submit. ", TicketType.Bug, Guid.CreateVersion7(),
            TicketPriority.High, reporterId, Now);

        Assert.Equal(TicketStatus.Backlog, ticket.Status);
        Assert.Equal(TicketType.Bug, ticket.Type);
        Assert.Equal("Login fails", ticket.Title);
        Assert.Equal("Error 500 on submit.", ticket.Description);
        Assert.Null(ticket.AssignedToId);
        Assert.Null(ticket.ClosedAtUtc);
        Assert.False(ticket.IsClosed);
        Assert.Equal(Now, ticket.CreatedAtUtc);
        var entry = Assert.Single(ticket.History);
        Assert.Equal(TicketChangeType.Created, entry.ChangeType);
        Assert.Equal("Backlog", entry.NewValue);
        Assert.Equal(reporterId, entry.ChangedById);
        Assert.Equal(ticket.Id, entry.TicketId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_BlankTitle_Throws(string title) =>
        Assert.ThrowsAny<ArgumentException>(() =>
            Ticket.Create(title, "desc", TicketType.Task, Guid.CreateVersion7(), TicketPriority.Low, Guid.CreateVersion7(), Now));

    [Fact]
    public void Create_TitleTooLong_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Ticket.Create(new string('x', Ticket.TitleMaxLength + 1), "desc", TicketType.Task, Guid.CreateVersion7(),
                TicketPriority.Low, Guid.CreateVersion7(), Now));

    [Fact]
    public void Create_GeneratesVersion7Ids()
    {
        // v7 GUIDs carry a millisecond timestamp prefix, which keeps PostgreSQL B-tree inserts mostly sequential.
        var ticket = Ticket.Create("a", "b", TicketType.Story, Guid.CreateVersion7(), TicketPriority.Low, Guid.CreateVersion7(), Now);

        Assert.Equal(7, ticket.Id.Version);
        Assert.Equal(7, Assert.Single(ticket.History).Id.Version);
    }
}

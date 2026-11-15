using OpenTicket.Domain.Tickets;

namespace OpenTicket.UnitTests.Domain;

public sealed class TicketCommentTests
{
    [Fact]
    public void Constructor_BodyTooLong_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new TicketComment(
            Guid.CreateVersion7(), Guid.CreateVersion7(), new string('x', TicketComment.BodyMaxLength + 1), DateTime.UtcNow));

    [Fact]
    public void Constructor_BlankBody_Throws() =>
        Assert.ThrowsAny<ArgumentException>(() => new TicketComment(
            Guid.CreateVersion7(), Guid.CreateVersion7(), " ", DateTime.UtcNow));
}

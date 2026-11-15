namespace OpenTicket.Domain.Tickets;

/// <summary>A message in a ticket's conversation. Comments are immutable once written.</summary>
public sealed class TicketComment
{
    public const int BodyMaxLength = 4000;

    private TicketComment()
    {
        Body = string.Empty;
    }

    public TicketComment(Guid ticketId, Guid authorId, string body, DateTime createdAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(body);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(body.Length, BodyMaxLength, nameof(body));

        Id = Guid.CreateVersion7();
        TicketId = ticketId;
        AuthorId = authorId;
        Body = body;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid TicketId { get; private set; }

    public Guid AuthorId { get; private set; }

    public string Body { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
}

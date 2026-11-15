namespace OpenTicket.Domain.Tickets;

/// <summary>An append-only audit entry describing one change to a ticket.</summary>
public sealed class TicketHistory
{
    public const int ValueMaxLength = 256;

    private TicketHistory()
    {
    }

    internal TicketHistory(
        Guid ticketId,
        Guid changedById,
        TicketChangeType changeType,
        string? oldValue,
        string? newValue,
        DateTime createdAtUtc)
    {
        Id = Guid.CreateVersion7();
        TicketId = ticketId;
        ChangedById = changedById;
        ChangeType = changeType;
        OldValue = Truncate(oldValue);
        NewValue = Truncate(newValue);
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid TicketId { get; private set; }

    public Guid ChangedById { get; private set; }

    public TicketChangeType ChangeType { get; private set; }

    public string? OldValue { get; private set; }

    public string? NewValue { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    private static string? Truncate(string? value) =>
        value is { Length: > ValueMaxLength } ? value[..ValueMaxLength] : value;
}

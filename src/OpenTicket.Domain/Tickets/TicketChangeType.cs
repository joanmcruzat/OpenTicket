namespace OpenTicket.Domain.Tickets;

public enum TicketChangeType : short
{
    Created = 0,
    StatusChanged = 1,
    Assigned = 2,
    Unassigned = 3,
    PriorityChanged = 4,
    CategoryChanged = 5,
    DetailsEdited = 6,
    TypeChanged = 7,
    SprintChanged = 8,
}

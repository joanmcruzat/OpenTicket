namespace OpenTicket.Domain.Tickets;

/// <summary>
/// The development workflow (docs/architecture.md §11). <see cref="Done"/> and <see cref="WontDo"/> are the closed
/// statuses; their numeric values are the highest so queries can use <c>status &lt; Done</c> for "still open".
/// </summary>
public enum TicketStatus : short
{
    Backlog = 0,
    ToDo = 1,
    InProgress = 2,
    InReview = 3,
    Done = 4,
    WontDo = 5,
}

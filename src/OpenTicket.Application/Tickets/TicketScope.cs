using OpenTicket.Domain.Tickets;
using OpenTicket.Domain.Users;

namespace OpenTicket.Application.Tickets;

/// <summary>Row-level visibility: admins and developers see everything, reporters their own tickets.</summary>
public static class TicketScope
{
    public static IQueryable<Ticket> VisibleTo(this IQueryable<Ticket> tickets, Actor actor) => actor.Role switch
    {
        UserRole.Admin or UserRole.Agent => tickets,
        _ => tickets.Where(t => t.CreatedById == actor.UserId),
    };
}

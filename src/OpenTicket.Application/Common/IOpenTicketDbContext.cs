using Microsoft.EntityFrameworkCore;

using OpenTicket.Domain.Categories;
using OpenTicket.Domain.Sprints;
using OpenTicket.Domain.Tickets;

namespace OpenTicket.Application.Common;

/// <summary>The application's view of the database: domain sets only, no Identity tables.</summary>
public interface IOpenTicketDbContext
{
    DbSet<Ticket> Tickets { get; }

    DbSet<TicketComment> TicketComments { get; }

    DbSet<TicketHistory> TicketHistory { get; }

    DbSet<TicketCategory> TicketCategories { get; }

    DbSet<Sprint> Sprints { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

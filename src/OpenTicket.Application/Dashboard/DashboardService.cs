using Microsoft.EntityFrameworkCore;

using OpenTicket.Application.Common;
using OpenTicket.Application.Tickets;
using OpenTicket.Domain.Tickets;

namespace OpenTicket.Application.Dashboard;

public sealed record StatusCount(TicketStatus Status, int Count);

/// <param name="ByStatus">One entry per status, in workflow order, including zero counts.</param>
/// <param name="AssignedToMe">Developers only: their tickets that are not closed.</param>
/// <param name="Unassigned">Admins and developers: open tickets nobody has picked up.</param>
public sealed record DashboardSummary(int Total, IReadOnlyList<StatusCount> ByStatus, int? AssignedToMe, int? Unassigned);

public sealed class DashboardService(IOpenTicketDbContext db, ICurrentUser currentUser)
{
    /// <summary>Counts in the caller's scope.</summary>
    public async Task<DashboardSummary> GetSummaryAsync(CancellationToken ct)
    {
        var actor = currentUser.Actor;
        var visible = db.Tickets.VisibleTo(actor);
        var counts = await visible
            .GroupBy(t => t.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, ct);

        var byStatus = Enum.GetValues<TicketStatus>().Select(s => new StatusCount(s, counts.GetValueOrDefault(s))).ToList();
        int? assignedToMe = actor.IsAgent
            ? await visible.CountAsync(t => t.AssignedToId == actor.UserId && t.Status < TicketStatus.Done, ct)
            : null;
        int? unassigned = actor.IsCustomer
            ? null
            : await visible.CountAsync(t => t.AssignedToId == null && t.Status < TicketStatus.Done, ct);
        return new DashboardSummary(counts.Values.Sum(), byStatus, assignedToMe, unassigned);
    }
}

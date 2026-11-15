using Microsoft.EntityFrameworkCore;

using OpenTicket.Application.Common;
using OpenTicket.Domain.Sprints;
using OpenTicket.Domain.Tickets;

namespace OpenTicket.Application.Sprints;

/// <summary>Read helpers shared by the ticket and sprint services.</summary>
public sealed class SprintQueries(IOpenTicketDbContext db)
{
    public async Task<SprintSummary?> GetActiveAsync(CancellationToken ct)
    {
        var active = await db.Sprints.AsNoTracking().SingleOrDefaultAsync(s => s.State == SprintState.Active, ct);
        return active is null ? null : (await SummarizeAsync([active], ct))[0];
    }

    public async Task<IReadOnlyDictionary<Guid, string>> NamesAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var wanted = ids.Where(id => id != Guid.Empty).Distinct().ToList();
        if (wanted.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        return await db.Sprints.AsNoTracking().Where(s => wanted.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name, ct);
    }

    /// <summary>Adds ticket counts (one grouped query) to each sprint, keeping the given order.</summary>
    public async Task<IReadOnlyList<SprintSummary>> SummarizeAsync(IReadOnlyList<Sprint> sprints, CancellationToken ct)
    {
        var ids = sprints.Select(s => s.Id).ToList();
        var counts = await db.Tickets.AsNoTracking()
            .Where(t => t.SprintId != null && ids.Contains(t.SprintId.Value))
            .GroupBy(t => t.SprintId!.Value)
            .Select(g => new { Id = g.Key, Total = g.Count(), Done = g.Count(t => t.Status >= TicketStatus.Done) })
            .ToDictionaryAsync(x => x.Id, ct);

        return sprints
            .Select(s => new SprintSummary(
                s.Id, s.Name, s.Goal, s.State, s.StartDate, s.EndDate, s.StartedAtUtc, s.CompletedAtUtc,
                counts.TryGetValue(s.Id, out var c) ? c.Total : 0,
                counts.TryGetValue(s.Id, out var d) ? d.Done : 0))
            .ToList();
    }
}

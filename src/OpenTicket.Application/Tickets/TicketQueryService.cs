using System.Globalization;

using Microsoft.EntityFrameworkCore;

using OpenTicket.Application.Common;
using OpenTicket.Application.Sprints;
using OpenTicket.Domain.Common;
using OpenTicket.Domain.Tickets;

namespace OpenTicket.Application.Tickets;

/// <summary>Read side for tickets. Every list query starts from <see cref="TicketScope.VisibleTo"/>.</summary>
public sealed class TicketQueryService(
    IOpenTicketDbContext db, IUserDirectory directory, ICurrentUser currentUser, SprintQueries sprints)
{
    public async Task<Result<PagedResult<TicketSummary>>> ListAsync(TicketListQuery query, CancellationToken ct)
    {
        var filtered = ApplyFilters(db.Tickets.VisibleTo(currentUser.Actor), query);
        var sorted = TicketSorting.Apply(filtered, query.SortBy, query.Descending);
        if (!sorted.IsSuccess)
        {
            return sorted.Error!;
        }

        var page = new PageRequest(query.Page, query.PageSize);
        var total = await filtered.CountAsync(ct);
        var items = await ToSummariesAsync(sorted.Value!.Skip(page.Skip).Take(page.PageSize), ct);
        return new PagedResult<TicketSummary>(items, page.Page, page.PageSize, total);
    }

    /// <summary>
    /// One column per status except Won't Do, limited to the active sprint when there is one. Open columns are
    /// ordered by priority (highest first) then number; Done shows the most recently finished tickets.
    /// </summary>
    public async Task<TicketBoard> GetBoardAsync(TicketBoardQuery query, CancellationToken ct)
    {
        var active = await sprints.GetActiveAsync(ct);
        var filtered = ApplyFilters(
            db.Tickets.VisibleTo(currentUser.Actor),
            new TicketListQuery(1, 1, null, query.Types, null, query.CategoryId, query.AssignedToId, null,
                query.Unassigned, query.Search, null, false, active?.Id));

        var totals = await filtered.GroupBy(t => t.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, ct);

        var columns = new List<TicketBoardColumn>();
        foreach (var status in BoardStatuses)
        {
            var inColumn = filtered.Where(t => t.Status == status);
            var ordered = status == TicketStatus.Done
                ? inColumn.OrderByDescending(t => t.UpdatedAtUtc).ThenByDescending(t => t.Id)
                : inColumn.OrderByDescending(t => t.Priority).ThenBy(t => t.Number);
            var items = await ToSummariesAsync(ordered.Take(TicketBoardQuery.ColumnLimit), ct);
            columns.Add(new TicketBoardColumn(status, totals.GetValueOrDefault(status), items));
        }

        return new TicketBoard(active, columns);
    }

    public async Task<Result<TicketDetail>> GetAsync(Guid ticketId, CancellationToken ct)
    {
        var ticket = await db.Tickets.AsNoTracking().SingleOrDefaultAsync(t => t.Id == ticketId, ct);
        if (ticket is null || !ticket.IsVisibleTo(currentUser.Actor))
        {
            return TicketErrors.NotFound;
        }

        return await ToDetailAsync(ticket, ct);
    }

    public async Task<Result<IReadOnlyList<TicketHistoryEntry>>> GetHistoryAsync(Guid ticketId, CancellationToken ct)
    {
        var ticket = await db.Tickets.AsNoTracking().SingleOrDefaultAsync(t => t.Id == ticketId, ct);
        if (ticket is null || !ticket.IsVisibleTo(currentUser.Actor))
        {
            return TicketErrors.NotFound;
        }

        var entries = await db.TicketHistory.AsNoTracking()
            .Where(h => h.TicketId == ticketId)
            .OrderBy(h => h.CreatedAtUtc).ThenBy(h => h.Id)
            .ToListAsync(ct);

        var userIds = entries.Select(e => e.ChangedById)
            .Concat(entries.Where(IsAssignment).SelectMany(e => ParseIds(e.OldValue, e.NewValue)));
        var users = await directory.GetSummariesAsync(userIds, ct);
        var categoryIds = entries.Where(e => e.ChangeType == TicketChangeType.CategoryChanged)
            .SelectMany(e => ParseIds(e.OldValue, e.NewValue)).Distinct().ToList();
        var categories = await db.TicketCategories.AsNoTracking()
            .Where(c => categoryIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, ct);
        var sprintNames = await sprints.NamesAsync(
            entries.Where(e => e.ChangeType == TicketChangeType.SprintChanged).SelectMany(e => ParseIds(e.OldValue, e.NewValue)), ct);

        string? Display(TicketHistory entry, string? value) => entry.ChangeType switch
        {
            _ when value is null => null,
            TicketChangeType.Assigned or TicketChangeType.Unassigned =>
                Guid.TryParse(value, out var id) ? users.NameOf(id) ?? value : value,
            TicketChangeType.CategoryChanged =>
                Guid.TryParse(value, out var id) && categories.TryGetValue(id, out var name) ? name : value,
            TicketChangeType.SprintChanged =>
                Guid.TryParse(value, out var id) && sprintNames.TryGetValue(id, out var sprint) ? sprint : value,
            _ => value,
        };

        return entries
            .Select(e => new TicketHistoryEntry(
                e.Id, e.ChangeType, Display(e, e.OldValue), Display(e, e.NewValue),
                e.ChangedById, users.NameOf(e.ChangedById), e.CreatedAtUtc))
            .ToList();
    }

    internal async Task<TicketDetail> ToDetailAsync(Ticket ticket, CancellationToken ct)
    {
        var categoryName = await db.TicketCategories.AsNoTracking()
            .Where(c => c.Id == ticket.CategoryId).Select(c => c.Name).SingleAsync(ct);
        var users = await directory.GetSummariesAsync([ticket.CreatedById, ticket.AssignedToId ?? Guid.Empty], ct);
        var sprintNames = await sprints.NamesAsync(ticket.SprintId is { } id ? [id] : [], ct);
        var actor = currentUser.Actor;
        return new TicketDetail(
            ticket.Id, ticket.Number, ticket.Title, ticket.Description, ticket.Type, ticket.Status, ticket.Priority,
            ticket.CategoryId, categoryName, ticket.CreatedById, users.NameOf(ticket.CreatedById),
            ticket.AssignedToId, users.NameOf(ticket.AssignedToId), ticket.CreatedAtUtc, ticket.UpdatedAtUtc,
            ticket.ClosedAtUtc, ticket.ClosureNote, ticket.Version, ticket.AllowedStatuses(actor), ticket.CanEdit(actor),
            ticket.SprintId, NameOf(sprintNames, ticket.SprintId));
    }

    private static readonly TicketStatus[] BoardStatuses =
        [TicketStatus.Backlog, TicketStatus.ToDo, TicketStatus.InProgress, TicketStatus.InReview, TicketStatus.Done];

    /// <summary>Runs an ordered, paged ticket query and maps it to summaries with category, user and sprint names.</summary>
    internal async Task<IReadOnlyList<TicketSummary>> ToSummariesAsync(IQueryable<Ticket> tickets, CancellationToken ct)
    {
        var rows = await tickets
            .Join(db.TicketCategories, t => t.CategoryId, c => c.Id, (t, c) => new { Ticket = t, CategoryName = c.Name })
            .AsNoTracking()
            .ToListAsync(ct);

        var users = await directory.GetSummariesAsync(
            rows.SelectMany(r => new[] { r.Ticket.CreatedById, r.Ticket.AssignedToId ?? Guid.Empty }), ct);
        var sprintNames = await sprints.NamesAsync(rows.Select(r => r.Ticket.SprintId ?? Guid.Empty), ct);
        var actor = currentUser.Actor;
        return rows
            .Select(r => new TicketSummary(
                r.Ticket.Id, r.Ticket.Number, r.Ticket.Title, r.Ticket.Type, r.Ticket.Status, r.Ticket.Priority,
                r.Ticket.CategoryId, r.CategoryName, r.Ticket.CreatedById, users.NameOf(r.Ticket.CreatedById),
                r.Ticket.AssignedToId, users.NameOf(r.Ticket.AssignedToId), r.Ticket.CreatedAtUtc, r.Ticket.UpdatedAtUtc,
                r.Ticket.Version, r.Ticket.AllowedStatuses(actor), r.Ticket.SprintId, NameOf(sprintNames, r.Ticket.SprintId)))
            .ToList();
    }

    private static string? NameOf(IReadOnlyDictionary<Guid, string> names, Guid? id) =>
        id is { } value && names.TryGetValue(value, out var name) ? name : null;

    private static IQueryable<Ticket> ApplyFilters(IQueryable<Ticket> query, TicketListQuery filter)
    {
        if (filter.Statuses is { Count: > 0 } statuses)
        {
            query = query.Where(t => statuses.Contains(t.Status));
        }

        if (filter.Types is { Count: > 0 } types)
        {
            query = query.Where(t => types.Contains(t.Type));
        }

        if (filter.Priorities is { Count: > 0 } priorities)
        {
            query = query.Where(t => priorities.Contains(t.Priority));
        }

        if (filter.CategoryId is { } categoryId)
        {
            query = query.Where(t => t.CategoryId == categoryId);
        }

        if (filter.AssignedToId is { } assignedToId)
        {
            query = query.Where(t => t.AssignedToId == assignedToId);
        }

        if (filter.CreatedById is { } createdById)
        {
            query = query.Where(t => t.CreatedById == createdById);
        }

        if (filter.SprintId is { } sprintId)
        {
            query = query.Where(t => t.SprintId == sprintId);
        }

        if (filter.Unplanned)
        {
            query = query.Where(t => t.SprintId == null);
        }

        if (filter.Unassigned)
        {
            query = query.Where(t => t.AssignedToId == null);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            var lowered = term.ToLowerInvariant();
            var numberText = term.StartsWith("OT-", StringComparison.OrdinalIgnoreCase) ? term[3..] : term;
            // Translated to SQL (lower(title) + strpos); .NET culture rules do not apply inside the query.
#pragma warning disable CA1304, CA1311, CA1862
            query = int.TryParse(numberText, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
                ? query.Where(t => t.Number == number || t.Title.ToLower().Contains(lowered))
                : query.Where(t => t.Title.ToLower().Contains(lowered));
#pragma warning restore CA1304, CA1311, CA1862
        }

        return query;
    }

    private static bool IsAssignment(TicketHistory entry) =>
        entry.ChangeType is TicketChangeType.Assigned or TicketChangeType.Unassigned;

    private static IEnumerable<Guid> ParseIds(params string?[] values)
    {
        foreach (var value in values)
        {
            if (Guid.TryParse(value, out var id))
            {
                yield return id;
            }
        }
    }
}

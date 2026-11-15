using OpenTicket.Domain.Common;
using OpenTicket.Domain.Tickets;

namespace OpenTicket.Application.Tickets;

/// <summary>Allow-listed sort fields; client input never reaches the query as a column name.</summary>
internal static class TicketSorting
{
    public static readonly IReadOnlyList<string> Fields = ["createdAt", "updatedAt", "priority", "status", "number"];

    public static Result<IQueryable<Ticket>> Apply(IQueryable<Ticket> query, string? sortBy, bool descending)
    {
        IOrderedQueryable<Ticket>? ordered = (sortBy ?? "createdAt").ToUpperInvariant() switch
        {
            "CREATEDAT" => descending ? query.OrderByDescending(t => t.CreatedAtUtc) : query.OrderBy(t => t.CreatedAtUtc),
            "UPDATEDAT" => descending ? query.OrderByDescending(t => t.UpdatedAtUtc) : query.OrderBy(t => t.UpdatedAtUtc),
            "PRIORITY" => descending ? query.OrderByDescending(t => t.Priority) : query.OrderBy(t => t.Priority),
            "STATUS" => descending ? query.OrderByDescending(t => t.Status) : query.OrderBy(t => t.Status),
            "NUMBER" => descending ? query.OrderByDescending(t => t.Number) : query.OrderBy(t => t.Number),
            _ => null,
        };

        if (ordered is null)
        {
            return Error.Validation("ticket.invalid_sort", "The sort field is not supported.",
                new Dictionary<string, string[]> { ["sortBy"] = [$"Use one of: {string.Join(", ", Fields)}."] });
        }

        // Deterministic paging: break ties on the unique id.
        return Result<IQueryable<Ticket>>.Success(descending ? ordered.ThenByDescending(t => t.Id) : ordered.ThenBy(t => t.Id));
    }
}

using Microsoft.EntityFrameworkCore;

using OpenTicket.Application.Common;
using OpenTicket.Domain.Common;
using OpenTicket.Domain.Tickets;
using OpenTicket.Domain.Users;

namespace OpenTicket.Application.Comments;

public sealed record CommentView(Guid Id, Guid TicketId, Guid AuthorId, string? AuthorName, UserRole? AuthorRole, string Body, DateTime CreatedAtUtc);

/// <summary>Ticket conversation. Visibility follows the ticket; comments are immutable.</summary>
public sealed class CommentService(IOpenTicketDbContext db, IUserDirectory directory, ICurrentUser currentUser, TimeProvider clock)
{
    public async Task<Result<PagedResult<CommentView>>> ListAsync(Guid ticketId, PageRequest page, CancellationToken ct)
    {
        var ticket = await db.Tickets.AsNoTracking().SingleOrDefaultAsync(t => t.Id == ticketId, ct);
        if (ticket is null || !ticket.IsVisibleTo(currentUser.Actor))
        {
            return TicketErrors.NotFound;
        }

        var query = db.TicketComments.AsNoTracking().Where(c => c.TicketId == ticketId);
        var total = await query.CountAsync(ct);
        var comments = await query
            .OrderBy(c => c.CreatedAtUtc).ThenBy(c => c.Id)
            .Skip(page.Skip).Take(page.PageSize)
            .ToListAsync(ct);
        var users = await directory.GetSummariesAsync(comments.Select(c => c.AuthorId), ct);
        return new PagedResult<CommentView>(comments.Select(c => ToView(c, users)).ToList(), page.Page, page.PageSize, total);
    }

    public async Task<Result<CommentView>> AddAsync(Guid ticketId, string body, CancellationToken ct)
    {
        var actor = currentUser.Actor;
        var ticket = await db.Tickets.AsNoTracking().SingleOrDefaultAsync(t => t.Id == ticketId, ct);
        if (ticket is null)
        {
            return TicketErrors.NotFound;
        }

        var allowed = ticket.EnsureCanComment(actor);
        if (!allowed.IsSuccess)
        {
            return allowed.Error!;
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            return Error.Validation("comment.blank", "The comment cannot be blank.",
                new Dictionary<string, string[]> { ["body"] = ["The comment cannot be blank."] });
        }

        var comment = new TicketComment(ticket.Id, actor.UserId, body.Trim(), clock.GetUtcNow().UtcDateTime);
        db.TicketComments.Add(comment);
        await db.SaveChangesAsync(ct);
        var users = await directory.GetSummariesAsync([actor.UserId], ct);
        return ToView(comment, users);
    }

    private static CommentView ToView(TicketComment comment, IReadOnlyDictionary<Guid, UserSummary> users) =>
        new(comment.Id, comment.TicketId, comment.AuthorId, users.NameOf(comment.AuthorId),
            users.TryGetValue(comment.AuthorId, out var author) ? author.Role : null, comment.Body, comment.CreatedAtUtc);
}

using Microsoft.EntityFrameworkCore;

using OpenTicket.Application.Common;
using OpenTicket.Domain.Categories;
using OpenTicket.Domain.Common;

namespace OpenTicket.Application.Categories;

public sealed record CategoryView(Guid Id, string Name, string? Description, bool IsActive, DateTime CreatedAtUtc, DateTime UpdatedAtUtc);

public static class CategoryErrors
{
    public static readonly Error NotFound = Error.NotFound("category.not_found", "The category was not found.");

    public static readonly Error DuplicateName =
        Error.Conflict("category.duplicate_name", "A category with this name already exists.");

    public static Error Blank => Error.Validation("category.name_blank", "The category name cannot be blank.",
        new Dictionary<string, string[]> { ["name"] = ["The category name cannot be blank."] });
}

/// <summary>Categories. Reads are open to every signed-in user; writes are admin-only (enforced by endpoint policy).</summary>
public sealed class CategoryService(IOpenTicketDbContext db, ICurrentUser currentUser, TimeProvider clock)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    /// <summary>Non-admins always get active categories only.</summary>
    public async Task<IReadOnlyList<CategoryView>> ListAsync(bool includeInactive, CancellationToken ct)
    {
        var query = db.TicketCategories.AsNoTracking();
        if (!(includeInactive && currentUser.Actor.IsAdmin))
        {
            query = query.Where(c => c.IsActive);
        }

        return await query.OrderBy(c => c.Name).Select(c => ToView(c)).ToListAsync(ct);
    }

    public async Task<Result<CategoryView>> CreateAsync(string name, string? description, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return CategoryErrors.Blank;
        }

        if (await NameTakenAsync(name, exceptId: null, ct))
        {
            return CategoryErrors.DuplicateName;
        }

        var category = new TicketCategory(name, description, Now);
        db.TicketCategories.Add(category);
        await db.SaveChangesAsync(ct);
        return ToView(category);
    }

    public async Task<Result<CategoryView>> UpdateAsync(Guid id, string name, string? description, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return CategoryErrors.Blank;
        }

        var category = await db.TicketCategories.SingleOrDefaultAsync(c => c.Id == id, ct);
        if (category is null)
        {
            return CategoryErrors.NotFound;
        }

        if (await NameTakenAsync(name, exceptId: id, ct))
        {
            return CategoryErrors.DuplicateName;
        }

        category.Update(name, description, Now);
        await db.SaveChangesAsync(ct);
        return ToView(category);
    }

    public async Task<Result<CategoryView>> SetActiveAsync(Guid id, bool isActive, CancellationToken ct)
    {
        var category = await db.TicketCategories.SingleOrDefaultAsync(c => c.Id == id, ct);
        if (category is null)
        {
            return CategoryErrors.NotFound;
        }

        if (isActive)
        {
            category.Activate(Now);
        }
        else
        {
            category.Deactivate(Now);
        }

        await db.SaveChangesAsync(ct);
        return ToView(category);
    }

    private Task<bool> NameTakenAsync(string name, Guid? exceptId, CancellationToken ct)
    {
        var normalized = TicketCategory.Normalize(name);
        return db.TicketCategories.AnyAsync(c => c.NormalizedName == normalized && c.Id != exceptId, ct);
    }

    private static CategoryView ToView(TicketCategory c) =>
        new(c.Id, c.Name, c.Description, c.IsActive, c.CreatedAtUtc, c.UpdatedAtUtc);
}

using OpenTicket.Domain.Categories;
using OpenTicket.Domain.Common;
using OpenTicket.Domain.Users;

namespace OpenTicket.Domain.Tickets;

/// <summary>
/// A work item (bug, task, story or improvement) and the single place that enforces its workflow and permissions
/// (docs/architecture.md §11). Every change appends a <see cref="TicketHistory"/> entry.
/// Callers must check <see cref="IsVisibleTo"/> first and answer "not found" when it is false.
/// </summary>
public sealed partial class Ticket
{
    public const int TitleMaxLength = 200;
    public const int DescriptionMaxLength = 8000;
    public const int ClosureNoteMaxLength = 1000;

    private readonly List<TicketHistory> _history = [];

    private Ticket()
    {
        Title = string.Empty;
        Description = string.Empty;
    }

    private Ticket(
        string title, string description, TicketType type, Guid categoryId, TicketPriority priority, Guid createdById, DateTime nowUtc)
    {
        Id = Guid.CreateVersion7();
        Title = title;
        Description = description;
        Type = type;
        CategoryId = categoryId;
        Priority = priority;
        CreatedById = createdById;
        Status = TicketStatus.Backlog;
        CreatedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    public Guid Id { get; private set; }

    /// <summary>Human-readable sequence number, assigned by the database (shown as OT-42).</summary>
    public int Number { get; private set; }

    public string Title { get; private set; }

    public string Description { get; private set; }

    public TicketType Type { get; private set; }

    public TicketStatus Status { get; private set; }

    public TicketPriority Priority { get; private set; }

    public Guid CategoryId { get; private set; }

    public Guid CreatedById { get; private set; }

    public Guid? AssignedToId { get; private set; }

    /// <summary>The sprint the ticket is planned into; null means it is in the backlog.</summary>
    public Guid? SprintId { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    /// <summary>When the ticket reached Done or Won't Do; cleared when it is reopened.</summary>
    public DateTime? ClosedAtUtc { get; private set; }

    /// <summary>The required reason when the ticket is Won't Do.</summary>
    public string? ClosureNote { get; private set; }

    /// <summary>Optimistic concurrency token (PostgreSQL <c>xmin</c>).</summary>
    public uint Version { get; private set; }

    public IReadOnlyCollection<TicketHistory> History => _history.AsReadOnly();

    public bool IsClosed => Status is TicketStatus.Done or TicketStatus.WontDo;

    public static Ticket Create(
        string title,
        string description,
        TicketType type,
        Guid categoryId,
        TicketPriority priority,
        Guid createdById,
        DateTime nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(title.Length, TitleMaxLength, nameof(title));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(description.Length, DescriptionMaxLength, nameof(description));

        var ticket = new Ticket(title.Trim(), description.Trim(), type, categoryId, priority, createdById, nowUtc);
        ticket.Record(createdById, TicketChangeType.Created, null, TicketStatus.Backlog.ToString(), nowUtc);
        return ticket;
    }

    /// <summary>Admins and developers see every ticket; reporters see the ones they created.</summary>
    public bool IsVisibleTo(Actor actor) =>
        actor.IsAdmin || actor.IsAgent || IsOwner(actor);

    /// <summary>Anyone who can see the ticket can comment until it is closed.</summary>
    public Result EnsureCanComment(Actor actor)
    {
        if (!IsVisibleTo(actor))
        {
            return TicketErrors.NotFound;
        }

        return IsClosed ? TicketErrors.Closed : Result.Success();
    }

    /// <summary>Admins edit any open ticket, developers the ones assigned to them, reporters their own until work starts.</summary>
    public bool CanEdit(Actor actor) =>
        !IsClosed && (actor.IsAdmin || IsAssignee(actor) || (IsOwner(actor) && !IsStarted));

    /// <summary>Partial update. Null arguments mean "unchanged"; <paramref name="category"/> is the new category, if any.</summary>
    public Result UpdateDetails(Actor actor, TicketDetailsChange change, TicketCategory? category, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (IsClosed)
        {
            return TicketErrors.Closed;
        }

        if (!CanEdit(actor))
        {
            return IsOwner(actor) ? TicketErrors.ReporterEditOnlyBeforeWork : TicketErrors.NotAllowed;
        }

        if (change.Title is not null && string.IsNullOrWhiteSpace(change.Title))
        {
            return TicketErrors.Blank("title");
        }

        if (change.Description is not null && string.IsNullOrWhiteSpace(change.Description))
        {
            return TicketErrors.Blank("description");
        }

        if (category is { IsActive: false })
        {
            return TicketErrors.CategoryInactive;
        }

        ApplyTextChanges(actor, change, nowUtc);
        if (change.Type is { } newType && newType != Type)
        {
            Record(actor.UserId, TicketChangeType.TypeChanged, Type.ToString(), newType.ToString(), nowUtc);
            Type = newType;
            UpdatedAtUtc = nowUtc;
        }

        if (change.Priority is { } newPriority && newPriority != Priority)
        {
            Record(actor.UserId, TicketChangeType.PriorityChanged, Priority.ToString(), newPriority.ToString(), nowUtc);
            Priority = newPriority;
            UpdatedAtUtc = nowUtc;
        }

        if (category is not null && category.Id != CategoryId)
        {
            Record(actor.UserId, TicketChangeType.CategoryChanged, CategoryId.ToString(), category.Id.ToString(), nowUtc);
            CategoryId = category.Id;
            UpdatedAtUtc = nowUtc;
        }

        return Result.Success();
    }

    private void ApplyTextChanges(Actor actor, TicketDetailsChange change, DateTime nowUtc)
    {
        if (change.Title is not null && change.Title.Trim() != Title)
        {
            Record(actor.UserId, TicketChangeType.DetailsEdited, Title, change.Title.Trim(), nowUtc);
            Title = change.Title.Trim();
            UpdatedAtUtc = nowUtc;
        }

        if (change.Description is not null && change.Description.Trim() != Description)
        {
            Record(actor.UserId, TicketChangeType.DetailsEdited, "description", "description", nowUtc);
            Description = change.Description.Trim();
            UpdatedAtUtc = nowUtc;
        }
    }

    /// <summary>Work has started once the ticket left Backlog and To Do.</summary>
    private bool IsStarted => Status is not (TicketStatus.Backlog or TicketStatus.ToDo);

    private bool IsOwner(Actor actor) => actor.IsCustomer && CreatedById == actor.UserId;

    private bool IsAssignee(Actor actor) => actor.IsAgent && AssignedToId == actor.UserId;

    private void Record(Guid actorId, TicketChangeType type, string? oldValue, string? newValue, DateTime nowUtc) =>
        _history.Add(new TicketHistory(Id, actorId, type, oldValue, newValue, nowUtc));
}

/// <summary>The fields a caller wants to change; null means "unchanged". The category is resolved separately.</summary>
public sealed record TicketDetailsChange(string? Title, string? Description, TicketType? Type, TicketPriority? Priority);

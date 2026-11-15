using OpenTicket.Domain.Tickets;

namespace OpenTicket.Application.Tickets;

/// <param name="AllowedStatuses">Statuses the caller may move this ticket to now (drives the board and status menu).</param>
public sealed record TicketSummary(
    Guid Id,
    int Number,
    string Title,
    TicketType Type,
    TicketStatus Status,
    TicketPriority Priority,
    Guid CategoryId,
    string CategoryName,
    Guid CreatedById,
    string? CreatedByName,
    Guid? AssignedToId,
    string? AssignedToName,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    uint Version,
    IReadOnlyList<TicketStatus> AllowedStatuses,
    Guid? SprintId,
    string? SprintName);

/// <param name="AllowedStatuses">Statuses the caller may move this ticket to now.</param>
/// <param name="CanEdit">Whether the caller may edit the title, description, type, priority and category.</param>
public sealed record TicketDetail(
    Guid Id,
    int Number,
    string Title,
    string Description,
    TicketType Type,
    TicketStatus Status,
    TicketPriority Priority,
    Guid CategoryId,
    string CategoryName,
    Guid CreatedById,
    string? CreatedByName,
    Guid? AssignedToId,
    string? AssignedToName,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime? ClosedAtUtc,
    string? ClosureNote,
    uint Version,
    IReadOnlyList<TicketStatus> AllowedStatuses,
    bool CanEdit,
    Guid? SprintId,
    string? SprintName);

public sealed record TicketHistoryEntry(
    Guid Id,
    TicketChangeType ChangeType,
    string? OldValue,
    string? NewValue,
    Guid ChangedById,
    string? ChangedByName,
    DateTime CreatedAtUtc);

public sealed record TicketListQuery(
    int Page,
    int PageSize,
    IReadOnlyList<TicketStatus>? Statuses,
    IReadOnlyList<TicketType>? Types,
    IReadOnlyList<TicketPriority>? Priorities,
    Guid? CategoryId,
    Guid? AssignedToId,
    Guid? CreatedById,
    bool Unassigned,
    string? Search,
    string? SortBy,
    bool Descending,
    Guid? SprintId = null,
    bool Unplanned = false);

/// <summary>Board filters. Columns hold at most <see cref="ColumnLimit"/> cards each.</summary>
public sealed record TicketBoardQuery(
    IReadOnlyList<TicketType>? Types, Guid? CategoryId, Guid? AssignedToId, bool Unassigned, string? Search)
{
    public const int ColumnLimit = 50;
}

/// <summary>The board: the active sprint's tickets, or every ticket when no sprint is active.</summary>
/// <param name="Sprint">The active sprint, or null when the board shows every ticket.</param>
public sealed record TicketBoard(Sprints.SprintSummary? Sprint, IReadOnlyList<TicketBoardColumn> Columns);

/// <param name="Total">All matching tickets in this status; <paramref name="Items"/> may hold fewer.</param>
public sealed record TicketBoardColumn(TicketStatus Status, int Total, IReadOnlyList<TicketSummary> Items);

public sealed record CreateTicketCommand(
    string Title, string Description, TicketType Type, Guid CategoryId, TicketPriority Priority);

public sealed record UpdateTicketCommand(
    Guid TicketId,
    uint Version,
    string? Title,
    string? Description,
    TicketType? Type,
    Guid? CategoryId,
    TicketPriority? Priority);

/// <param name="Reason">Required when <paramref name="Status"/> is Won't Do.</param>
public sealed record ChangeTicketStatusCommand(Guid TicketId, uint Version, TicketStatus Status, string? Reason);

/// <param name="SprintId">Null moves the ticket back to the backlog.</param>
public sealed record MoveTicketToSprintCommand(Guid TicketId, uint Version, Guid? SprintId);

public sealed record AssignTicketCommand(Guid TicketId, uint Version, Guid AssigneeId);

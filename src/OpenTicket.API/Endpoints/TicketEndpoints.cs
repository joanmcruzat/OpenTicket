using System.ComponentModel.DataAnnotations;

using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

using OpenTicket.API.ErrorHandling;
using OpenTicket.API.Security;
using OpenTicket.Application.Common;
using OpenTicket.Application.Tickets;
using OpenTicket.Domain.Tickets;

namespace OpenTicket.API.Endpoints;

public sealed class TicketListRequest
{
    [Range(1, 1_000_000)]
    public int? Page { get; init; }

    [Range(1, PageRequest.MaxPageSize)]
    public int? PageSize { get; init; }

    public TicketStatus[]? Status { get; init; }

    public TicketType[]? Type { get; init; }

    public TicketPriority[]? Priority { get; init; }

    public Guid? CategoryId { get; init; }

    public Guid? AssignedToId { get; init; }

    public Guid? CreatedById { get; init; }

    public Guid? SprintId { get; init; }

    /// <summary>Only tickets that are in no sprint.</summary>
    public bool? Unplanned { get; init; }

    public bool? Unassigned { get; init; }

    [MaxLength(100)]
    public string? Search { get; init; }

    [MaxLength(20)]
    public string? SortBy { get; init; }

    [RegularExpression("^(asc|desc|ASC|DESC)$", ErrorMessage = "Use 'asc' or 'desc'.")]
    public string? SortDir { get; init; }
}

public sealed class TicketBoardRequest
{
    public TicketType[]? Type { get; init; }

    public Guid? CategoryId { get; init; }

    public Guid? AssignedToId { get; init; }

    public bool? Unassigned { get; init; }

    [MaxLength(100)]
    public string? Search { get; init; }
}

/// <param name="Type">Defaults to Task when omitted.</param>
public sealed record CreateTicketRequest(
    [Required, MaxLength(Ticket.TitleMaxLength)] string Title,
    [Required, MaxLength(Ticket.DescriptionMaxLength)] string Description,
    TicketType? Type,
    [Required] Guid? CategoryId,
    [Required] TicketPriority? Priority);

public sealed record UpdateTicketRequest(
    [MaxLength(Ticket.TitleMaxLength)] string? Title,
    [MaxLength(Ticket.DescriptionMaxLength)] string? Description,
    TicketType? Type,
    Guid? CategoryId,
    TicketPriority? Priority,
    [Required] uint? Version);

/// <param name="Reason">Required when moving to WontDo.</param>
public sealed record ChangeTicketStatusRequest(
    [Required] TicketStatus? Status,
    [MaxLength(Ticket.ClosureNoteMaxLength)] string? Reason,
    [Required] uint? Version);

/// <param name="SprintId">Null moves the ticket back to the backlog.</param>
public sealed record MoveTicketToSprintRequest(Guid? SprintId, [Required] uint? Version);

public sealed record AssignTicketRequest([Required] Guid? AssigneeId, [Required] uint? Version);

internal static class TicketEndpoints
{
    public static RouteGroupBuilder MapTicketEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/tickets").WithTags("Tickets");

        group.MapGet("/", ListAsync).WithName("ListTickets").WithSummary("Tickets in the caller's scope, paged.");
        group.MapGet("/board", GetBoardAsync).WithName("GetTicketBoard")
            .WithSummary("The active sprint's tickets (or every ticket when no sprint is active) in board columns.");
        group.MapGet("/{ticketId:guid}", GetAsync).WithName("GetTicket");
        group.MapPost("/", CreateAsync).WithName("CreateTicket");
        group.MapPatch("/{ticketId:guid}", UpdateAsync).WithName("UpdateTicket");
        group.MapPost("/{ticketId:guid}/status", ChangeStatusAsync).WithName("ChangeTicketStatus")
            .WithSummary("Move the ticket to another status. Moving to WontDo needs a reason.");
        group.MapPut("/{ticketId:guid}/assignee", AssignAsync).WithName("AssignTicket")
            .WithSummary("Admins assign any developer; developers assign themselves to an unassigned ticket.")
            .RequireAuthorization(Policies.StaffOnly);
        group.MapDelete("/{ticketId:guid}/assignee", UnassignAsync).WithName("UnassignTicket")
            .RequireAuthorization(Policies.StaffOnly);
        group.MapPut("/{ticketId:guid}/sprint", MoveToSprintAsync).WithName("MoveTicketToSprint")
            .WithSummary("Plans the ticket into a sprint, or back to the backlog with sprintId null.")
            .RequireAuthorization(Policies.StaffOnly);
        group.MapGet("/{ticketId:guid}/history", GetHistoryAsync).WithName("GetTicketHistory");
        return group;
    }

    private static async Task<Results<Ok<PagedResult<TicketSummary>>, ProblemHttpResult>> ListAsync(
        [AsParameters] TicketListRequest request, TicketQueryService tickets, CancellationToken ct)
    {
        var query = new TicketListQuery(
            request.Page ?? 1,
            request.PageSize ?? 20,
            request.Status,
            request.Type,
            request.Priority,
            request.CategoryId,
            request.AssignedToId,
            request.CreatedById,
            request.Unassigned ?? false,
            request.Search,
            request.SortBy,
            !string.Equals(request.SortDir, "asc", StringComparison.OrdinalIgnoreCase),
            request.SprintId,
            request.Unplanned ?? false);
        return (await tickets.ListAsync(query, ct)).ToOk();
    }

    private static async Task<Ok<TicketBoard>> GetBoardAsync(
        [AsParameters] TicketBoardRequest request, TicketQueryService tickets, CancellationToken ct)
    {
        var query = new TicketBoardQuery(
            request.Type, request.CategoryId, request.AssignedToId, request.Unassigned ?? false, request.Search);
        return TypedResults.Ok(await tickets.GetBoardAsync(query, ct));
    }

    private static async Task<Results<Ok<TicketDetail>, ProblemHttpResult>> GetAsync(
        Guid ticketId, TicketQueryService tickets, CancellationToken ct) =>
        (await tickets.GetAsync(ticketId, ct)).ToOk();

    private static async Task<Results<Created<TicketDetail>, ProblemHttpResult>> CreateAsync(
        CreateTicketRequest request, TicketCommandService tickets, CancellationToken ct)
    {
        var result = await tickets.CreateAsync(
            new CreateTicketCommand(
                request.Title,
                request.Description,
                request.Type ?? TicketType.Task,
                request.CategoryId!.Value,
                request.Priority!.Value),
            ct);
        return result.IsSuccess
            ? TypedResults.Created($"/api/v1/tickets/{result.Value!.Id}", result.Value)
            : result.Error!.ToProblem();
    }

    private static async Task<Results<Ok<TicketDetail>, ProblemHttpResult>> UpdateAsync(
        Guid ticketId, UpdateTicketRequest request, TicketCommandService tickets, CancellationToken ct) =>
        (await tickets.UpdateAsync(new UpdateTicketCommand(
            ticketId,
            request.Version!.Value,
            request.Title,
            request.Description,
            request.Type,
            request.CategoryId,
            request.Priority), ct)).ToOk();

    private static async Task<Results<Ok<TicketDetail>, ProblemHttpResult>> ChangeStatusAsync(
        Guid ticketId, ChangeTicketStatusRequest request, TicketCommandService tickets, CancellationToken ct) =>
        (await tickets.ChangeStatusAsync(new ChangeTicketStatusCommand(
            ticketId, request.Version!.Value, request.Status!.Value, request.Reason), ct)).ToOk();

    private static async Task<Results<Ok<TicketDetail>, ProblemHttpResult>> MoveToSprintAsync(
        Guid ticketId, MoveTicketToSprintRequest request, TicketCommandService tickets, CancellationToken ct) =>
        (await tickets.MoveToSprintAsync(new MoveTicketToSprintCommand(ticketId, request.Version!.Value, request.SprintId), ct)).ToOk();

    private static async Task<Results<Ok<TicketDetail>, ProblemHttpResult>> AssignAsync(
        Guid ticketId, AssignTicketRequest request, TicketCommandService tickets, CancellationToken ct) =>
        (await tickets.AssignAsync(new AssignTicketCommand(ticketId, request.Version!.Value, request.AssigneeId!.Value), ct)).ToOk();

    private static async Task<Results<Ok<TicketDetail>, ProblemHttpResult>> UnassignAsync(
        Guid ticketId, [FromQuery, Required] uint? version, TicketCommandService tickets, CancellationToken ct) =>
        (await tickets.UnassignAsync(ticketId, version!.Value, ct)).ToOk();

    private static async Task<Results<Ok<IReadOnlyList<TicketHistoryEntry>>, ProblemHttpResult>> GetHistoryAsync(
        Guid ticketId, TicketQueryService tickets, CancellationToken ct) =>
        (await tickets.GetHistoryAsync(ticketId, ct)).ToOk();
}

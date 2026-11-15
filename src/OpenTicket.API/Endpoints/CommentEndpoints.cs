using System.ComponentModel.DataAnnotations;

using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

using OpenTicket.API.ErrorHandling;
using OpenTicket.Application.Comments;
using OpenTicket.Application.Common;
using OpenTicket.Domain.Tickets;

namespace OpenTicket.API.Endpoints;

public sealed record AddCommentRequest([Required, MaxLength(TicketComment.BodyMaxLength)] string Body);

internal static class CommentEndpoints
{
    public static RouteGroupBuilder MapCommentEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/tickets/{ticketId:guid}/comments").WithTags("Comments");
        group.MapGet("/", ListAsync).WithName("ListComments").WithSummary("Conversation, oldest first.");
        group.MapPost("/", AddAsync).WithName("AddComment");
        return group;
    }

    private static async Task<Results<Ok<PagedResult<CommentView>>, ProblemHttpResult>> ListAsync(
        Guid ticketId,
        [FromQuery, Range(1, 1_000_000)] int? page,
        [FromQuery, Range(1, PageRequest.MaxPageSize)] int? pageSize,
        CommentService comments,
        CancellationToken ct) =>
        (await comments.ListAsync(ticketId, new PageRequest(page ?? 1, pageSize ?? 50), ct)).ToOk();

    private static async Task<Results<Created<CommentView>, ProblemHttpResult>> AddAsync(
        Guid ticketId, AddCommentRequest request, CommentService comments, CancellationToken ct)
    {
        var result = await comments.AddAsync(ticketId, request.Body, ct);
        return result.IsSuccess
            ? TypedResults.Created($"/api/v1/tickets/{ticketId}/comments/{result.Value!.Id}", result.Value)
            : result.Error!.ToProblem();
    }
}

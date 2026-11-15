using System.ComponentModel.DataAnnotations;

using Microsoft.AspNetCore.Http.HttpResults;

using OpenTicket.API.ErrorHandling;
using OpenTicket.API.Security;
using OpenTicket.Application.Sprints;
using OpenTicket.Domain.Sprints;

namespace OpenTicket.API.Endpoints;

public sealed record CreateSprintRequest(
    [Required, MaxLength(Sprint.NameMaxLength)] string Name,
    [MaxLength(Sprint.GoalMaxLength)] string? Goal);

public sealed record UpdateSprintRequest(
    [Required, MaxLength(Sprint.NameMaxLength)] string Name,
    [MaxLength(Sprint.GoalMaxLength)] string? Goal,
    DateOnly? StartDate,
    DateOnly? EndDate);

public sealed record StartSprintRequest([Required] DateOnly? StartDate, [Required] DateOnly? EndDate);

/// <param name="MoveOpenTicketsTo">A planned sprint for unfinished tickets; null sends them to the backlog.</param>
public sealed record CompleteSprintRequest(Guid? MoveOpenTicketsTo);

internal static class SprintEndpoints
{
    public static RouteGroupBuilder MapSprintEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/sprints").WithTags("Sprints");

        // Everyone signed in can read sprints (reporters see which sprint their ticket is in); only staff plan.
        group.MapGet("/", ListAsync).WithName("ListSprints")
            .WithSummary("Active and planned sprints, then the most recently completed.");
        group.MapGet("/plan", GetPlanAsync).WithName("GetSprintPlan")
            .WithSummary("Planning view: each open sprint with its tickets, then the unplanned backlog.")
            .RequireAuthorization(Policies.StaffOnly);
        group.MapPost("/", CreateAsync).WithName("CreateSprint").RequireAuthorization(Policies.StaffOnly);
        group.MapPut("/{sprintId:guid}", UpdateAsync).WithName("UpdateSprint").RequireAuthorization(Policies.StaffOnly);
        group.MapPost("/{sprintId:guid}/start", StartAsync).WithName("StartSprint").RequireAuthorization(Policies.StaffOnly);
        group.MapPost("/{sprintId:guid}/complete", CompleteAsync).WithName("CompleteSprint")
            .WithSummary("Completes the active sprint, moving unfinished tickets to a planned sprint or the backlog.")
            .RequireAuthorization(Policies.StaffOnly);
        group.MapDelete("/{sprintId:guid}", DeleteAsync).WithName("DeleteSprint")
            .WithSummary("Deletes a planned sprint; its tickets return to the backlog.")
            .RequireAuthorization(Policies.StaffOnly);
        return group;
    }

    private static async Task<Ok<IReadOnlyList<SprintSummary>>> ListAsync(SprintService sprints, CancellationToken ct) =>
        TypedResults.Ok(await sprints.ListAsync(ct));

    private static async Task<Results<Ok<SprintPlan>, ProblemHttpResult>> GetPlanAsync(SprintService sprints, CancellationToken ct) =>
        (await sprints.GetPlanAsync(ct)).ToOk();

    private static async Task<Results<Created<SprintSummary>, ProblemHttpResult>> CreateAsync(
        CreateSprintRequest request, SprintService sprints, CancellationToken ct)
    {
        var result = await sprints.CreateAsync(new CreateSprintCommand(request.Name, request.Goal), ct);
        return result.IsSuccess
            ? TypedResults.Created($"/api/v1/sprints/{result.Value!.Id}", result.Value)
            : result.Error!.ToProblem();
    }

    private static async Task<Results<Ok<SprintSummary>, ProblemHttpResult>> UpdateAsync(
        Guid sprintId, UpdateSprintRequest request, SprintService sprints, CancellationToken ct) =>
        (await sprints.UpdateAsync(
            new UpdateSprintCommand(sprintId, request.Name, request.Goal, request.StartDate, request.EndDate), ct)).ToOk();

    private static async Task<Results<Ok<SprintSummary>, ProblemHttpResult>> StartAsync(
        Guid sprintId, StartSprintRequest request, SprintService sprints, CancellationToken ct) =>
        (await sprints.StartAsync(new StartSprintCommand(sprintId, request.StartDate!.Value, request.EndDate!.Value), ct)).ToOk();

    private static async Task<Results<Ok<SprintSummary>, ProblemHttpResult>> CompleteAsync(
        Guid sprintId, CompleteSprintRequest request, SprintService sprints, CancellationToken ct) =>
        (await sprints.CompleteAsync(new CompleteSprintCommand(sprintId, request.MoveOpenTicketsTo), ct)).ToOk();

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteAsync(
        Guid sprintId, SprintService sprints, CancellationToken ct)
    {
        var result = await sprints.DeleteAsync(sprintId, ct);
        return result.IsSuccess ? TypedResults.NoContent() : result.Error!.ToProblem();
    }
}

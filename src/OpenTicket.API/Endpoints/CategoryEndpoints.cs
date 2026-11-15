using System.ComponentModel.DataAnnotations;

using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

using OpenTicket.API.ErrorHandling;
using OpenTicket.API.Security;
using OpenTicket.Application.Categories;
using OpenTicket.Domain.Categories;

namespace OpenTicket.API.Endpoints;

public sealed record CategoryRequest(
    [Required, MaxLength(TicketCategory.NameMaxLength)] string Name,
    [MaxLength(TicketCategory.DescriptionMaxLength)] string? Description);

internal static class CategoryEndpoints
{
    public static RouteGroupBuilder MapCategoryEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/categories").WithTags("Categories");
        group.MapGet("/", ListAsync).WithName("ListCategories")
            .WithSummary("Active categories; admins may pass includeInactive=true.");
        group.MapPost("/", CreateAsync).WithName("CreateCategory").RequireAuthorization(Policies.AdminOnly);
        group.MapPut("/{categoryId:guid}", UpdateAsync).WithName("UpdateCategory").RequireAuthorization(Policies.AdminOnly);
        group.MapPost("/{categoryId:guid}/activate", (Guid categoryId, CategoryService categories, CancellationToken ct) =>
                SetActiveAsync(categoryId, true, categories, ct))
            .WithName("ActivateCategory").RequireAuthorization(Policies.AdminOnly);
        group.MapPost("/{categoryId:guid}/deactivate", (Guid categoryId, CategoryService categories, CancellationToken ct) =>
                SetActiveAsync(categoryId, false, categories, ct))
            .WithName("DeactivateCategory").RequireAuthorization(Policies.AdminOnly);
        return group;
    }

    private static async Task<Ok<IReadOnlyList<CategoryView>>> ListAsync(
        [FromQuery] bool? includeInactive, CategoryService categories, CancellationToken ct) =>
        TypedResults.Ok(await categories.ListAsync(includeInactive ?? false, ct));

    private static async Task<Results<Created<CategoryView>, ProblemHttpResult>> CreateAsync(
        CategoryRequest request, CategoryService categories, CancellationToken ct)
    {
        var result = await categories.CreateAsync(request.Name, request.Description, ct);
        return result.IsSuccess
            ? TypedResults.Created($"/api/v1/categories/{result.Value!.Id}", result.Value)
            : result.Error!.ToProblem();
    }

    private static async Task<Results<Ok<CategoryView>, ProblemHttpResult>> UpdateAsync(
        Guid categoryId, CategoryRequest request, CategoryService categories, CancellationToken ct) =>
        (await categories.UpdateAsync(categoryId, request.Name, request.Description, ct)).ToOk();

    private static async Task<Results<Ok<CategoryView>, ProblemHttpResult>> SetActiveAsync(
        Guid categoryId, bool isActive, CategoryService categories, CancellationToken ct) =>
        (await categories.SetActiveAsync(categoryId, isActive, ct)).ToOk();
}

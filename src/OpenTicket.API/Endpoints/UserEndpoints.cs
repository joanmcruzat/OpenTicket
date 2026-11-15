using System.ComponentModel.DataAnnotations;

using Microsoft.AspNetCore.Http.HttpResults;

using OpenTicket.API.ErrorHandling;
using OpenTicket.API.Security;
using OpenTicket.Application.Common;
using OpenTicket.Application.Users;
using OpenTicket.Domain.Users;
using OpenTicket.Infrastructure.Identity;

namespace OpenTicket.API.Endpoints;

public sealed class UserListRequest
{
    [Range(1, 1_000_000)]
    public int? Page { get; init; }

    [Range(1, PageRequest.MaxPageSize)]
    public int? PageSize { get; init; }

    public UserRole? Role { get; init; }

    public bool? IsActive { get; init; }

    [MaxLength(100)]
    public string? Search { get; init; }
}

public sealed record CreateUserRequest(
    [Required, EmailAddress, MaxLength(256)] string Email,
    [Required, MaxLength(ApplicationUser.DisplayNameMaxLength)] string DisplayName,
    [Required] UserRole? Role,
    [Required, MinLength(10), MaxLength(128)] string TemporaryPassword);

public sealed record ChangeRoleRequest([Required] UserRole? Role);

public sealed record ResetPasswordRequest([Required, MinLength(10), MaxLength(128)] string TemporaryPassword);

internal static class UserEndpoints
{
    public static RouteGroupBuilder MapUserEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/users").WithTags("Users").RequireAuthorization(Policies.AdminOnly);
        group.MapGet("/", ListAsync).WithName("ListUsers");
        group.MapGet("/agents", ListAgentsAsync).WithName("ListActiveAgents")
            .WithSummary("Active agents with their open-ticket count, for the assignment picker.");
        group.MapGet("/{userId:guid}", GetAsync).WithName("GetUser");
        group.MapPost("/", CreateAsync).WithName("CreateUser");
        group.MapPut("/{userId:guid}/role", ChangeRoleAsync).WithName("ChangeUserRole");
        group.MapPost("/{userId:guid}/activate", (Guid userId, IUserAdminService admin, CancellationToken ct) =>
            SetActiveAsync(userId, true, admin, ct)).WithName("ActivateUser");
        group.MapPost("/{userId:guid}/deactivate", (Guid userId, IUserAdminService admin, CancellationToken ct) =>
            SetActiveAsync(userId, false, admin, ct)).WithName("DeactivateUser");
        group.MapPost("/{userId:guid}/reset-password", ResetPasswordAsync).WithName("ResetUserPassword");
        return group;
    }

    private static async Task<Ok<PagedResult<UserListItem>>> ListAsync(
        [AsParameters] UserListRequest request, IUserAdminService admin, CancellationToken ct) =>
        TypedResults.Ok(await admin.ListAsync(
            new UserListQuery(request.Page ?? 1, request.PageSize ?? 20, request.Role, request.IsActive, request.Search), ct));

    private static async Task<Ok<IReadOnlyList<AgentOption>>> ListAgentsAsync(IUserAdminService admin, CancellationToken ct) =>
        TypedResults.Ok(await admin.ListActiveAgentsAsync(ct));

    private static async Task<Results<Ok<UserListItem>, ProblemHttpResult>> GetAsync(
        Guid userId, IUserAdminService admin, CancellationToken ct) =>
        (await admin.GetAsync(userId, ct)).ToOk();

    private static async Task<Results<Created<UserListItem>, ProblemHttpResult>> CreateAsync(
        CreateUserRequest request, IUserAdminService admin, CancellationToken ct)
    {
        var result = await admin.CreateAsync(
            new CreateUserCommand(request.Email, request.DisplayName, request.Role!.Value, request.TemporaryPassword), ct);
        return result.IsSuccess
            ? TypedResults.Created($"/api/v1/users/{result.Value!.Id}", result.Value)
            : result.Error!.ToProblem();
    }

    private static async Task<Results<Ok<UserListItem>, ProblemHttpResult>> ChangeRoleAsync(
        Guid userId, ChangeRoleRequest request, IUserAdminService admin, CancellationToken ct) =>
        (await admin.ChangeRoleAsync(userId, request.Role!.Value, ct)).ToOk();

    private static async Task<Results<Ok<UserListItem>, ProblemHttpResult>> SetActiveAsync(
        Guid userId, bool isActive, IUserAdminService admin, CancellationToken ct) =>
        (await admin.SetActiveAsync(userId, isActive, ct)).ToOk();

    private static async Task<Results<NoContent, ProblemHttpResult>> ResetPasswordAsync(
        Guid userId, ResetPasswordRequest request, IUserAdminService admin, CancellationToken ct)
    {
        var result = await admin.ResetPasswordAsync(userId, request.TemporaryPassword, ct);
        return result.IsSuccess ? TypedResults.NoContent() : result.Error!.ToProblem();
    }
}

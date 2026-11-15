using System.ComponentModel.DataAnnotations;

using Microsoft.AspNetCore.Http.HttpResults;

using OpenTicket.API.ErrorHandling;
using OpenTicket.API.Security;
using OpenTicket.Application.Auth;
using OpenTicket.Application.Common;
using OpenTicket.Domain.Common;
using OpenTicket.Infrastructure.Identity;

namespace OpenTicket.API.Endpoints;

public sealed record RegisterRequest(
    [Required, EmailAddress, MaxLength(256)] string Email,
    [Required, MinLength(10), MaxLength(128)] string Password,
    [Required, MaxLength(ApplicationUser.DisplayNameMaxLength)] string DisplayName);

public sealed record LoginRequest(
    [Required, MaxLength(256)] string Email,
    [Required, MaxLength(128)] string Password);

public sealed record ChangePasswordRequest(
    [Required, MaxLength(128)] string CurrentPassword,
    [Required, MinLength(10), MaxLength(128)] string NewPassword);

/// <summary>Access token for the Authorization header. The refresh token travels only in an HttpOnly cookie.</summary>
public sealed record SessionResponse(string AccessToken, DateTime ExpiresAtUtc, UserProfile User);

internal static class AuthEndpoints
{

    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/auth").WithTags("Auth");

        // Public: sign-up, sign-in, refresh and logout must work without an access token. All are rate limited.
        group.MapPost("/register", RegisterAsync).WithName("Register").AllowAnonymous().RequireRateLimiting(RateLimiting.AuthPolicy);
        group.MapPost("/login", LoginAsync).WithName("Login").AllowAnonymous().RequireRateLimiting(RateLimiting.AuthPolicy);
        group.MapPost("/refresh", RefreshAsync).WithName("RefreshSession").AllowAnonymous().RequireRateLimiting(RateLimiting.AuthPolicy);
        group.MapPost("/logout", LogoutAsync).WithName("Logout").AllowAnonymous().RequireRateLimiting(RateLimiting.AuthPolicy);

        group.MapGet("/me", GetMeAsync).WithName("GetCurrentUser");
        group.MapPost("/change-password", ChangePasswordAsync).WithName("ChangePassword")
            .WithSummary("Changes the caller's password and signs out every session.");

        group.MapAccountRecoveryEndpoints();
        group.MapExternalLoginEndpoints();
        return group;
    }

    private static async Task<Results<Created<UserProfile>, ProblemHttpResult>> RegisterAsync(
        RegisterRequest request, IAuthService auth, CancellationToken ct)
    {
        var result = await auth.RegisterAsync(new RegisterCommand(request.Email, request.Password, request.DisplayName), ct);
        return result.IsSuccess
            ? TypedResults.Created($"/api/v1/users/{result.Value!.Id}", result.Value)
            : result.Error!.ToProblem();
    }

    private static async Task<Results<Ok<SessionResponse>, ProblemHttpResult>> LoginAsync(
        LoginRequest request, IAuthService auth, HttpContext http, CancellationToken ct) =>
        ToSessionResponse(http, await auth.LoginAsync(new LoginCommand(request.Email, request.Password), ct));

    private static async Task<Results<Ok<SessionResponse>, ProblemHttpResult>> RefreshAsync(
        IAuthService auth, HttpContext http, CancellationToken ct)
    {
        var token = http.Request.Cookies[RefreshCookie.Name];
        if (string.IsNullOrEmpty(token))
        {
            return Error.Unauthorized("auth.no_session", "You are not signed in.").ToProblem();
        }

        return ToSessionResponse(http, await auth.RefreshAsync(token, ct));
    }

    private static async Task<NoContent> LogoutAsync(IAuthService auth, HttpContext http, CancellationToken ct)
    {
        await auth.LogoutAsync(http.Request.Cookies[RefreshCookie.Name], ct);
        RefreshCookie.Delete(http);
        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<UserProfile>, ProblemHttpResult>> GetMeAsync(
        ICurrentUser currentUser, IAuthService auth, CancellationToken ct)
    {
        var result = await auth.GetProfileAsync(currentUser.Actor.UserId, ct);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error!.ToProblem();
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> ChangePasswordAsync(
        ChangePasswordRequest request, ICurrentUser currentUser, IAuthService auth, HttpContext http, CancellationToken ct)
    {
        var result = await auth.ChangePasswordAsync(currentUser.Actor.UserId, request.CurrentPassword, request.NewPassword, ct);
        if (!result.IsSuccess)
        {
            return result.Error!.ToProblem();
        }

        RefreshCookie.Delete(http);
        return TypedResults.NoContent();
    }

    private static Results<Ok<SessionResponse>, ProblemHttpResult> ToSessionResponse(HttpContext http, Result<AuthSession> result)
    {
        if (!result.IsSuccess)
        {
            RefreshCookie.Delete(http);
            return result.Error!.ToProblem();
        }

        var session = result.Value!;
        RefreshCookie.Append(http, session);
        return TypedResults.Ok(new SessionResponse(session.AccessToken, session.AccessTokenExpiresAtUtc, session.User));
    }
}

/// <summary>The refresh token cookie: HttpOnly, Secure, SameSite=Strict, sent only to /api/v1/auth.</summary>
internal static class RefreshCookie
{
    public const string Name = "ot_refresh";
    private const string CookiePath = "/api/v1/auth";

    public static void Append(HttpContext http, AuthSession session) =>
        http.Response.Cookies.Append(Name, session.RefreshToken, Options(session.RefreshTokenExpiresAtUtc));

    public static void Delete(HttpContext http) => http.Response.Cookies.Delete(Name, Options(DateTimeOffset.UnixEpoch));

    private static CookieOptions Options(DateTimeOffset expires) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = CookiePath,
        Expires = expires,
        IsEssential = true,
    };
}

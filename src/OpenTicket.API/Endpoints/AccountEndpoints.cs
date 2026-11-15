using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.HttpResults;

using OpenTicket.API.ErrorHandling;
using OpenTicket.API.Security;
using OpenTicket.Application.Auth;
using OpenTicket.Domain.Users;

namespace OpenTicket.API.Endpoints;

public sealed record ForgotPasswordRequest([Required, EmailAddress, MaxLength(256)] string Email);

public sealed record ResetPasswordWithTokenRequest(
    [Required] Guid? UserId,
    [Required, MaxLength(2048)] string Token,
    [Required, MinLength(10), MaxLength(128)] string NewPassword);

/// <summary>Password reset by email and "Sign in with GitHub / Google". Every route here is public by necessity.</summary>
internal static class AccountEndpoints
{
    public static RouteGroupBuilder MapAccountRecoveryEndpoints(this RouteGroupBuilder auth)
    {
        auth.MapGet("/options", GetOptionsAsync).WithName("GetAuthOptions")
            .WithSummary("Which sign-in options this server offers (external providers, password reset).")
            .AllowAnonymous();
        auth.MapPost("/forgot-password", ForgotPasswordAsync).WithName("ForgotPassword")
            .WithSummary("Emails a reset link if the account exists. Always 202, so emails cannot be probed.")
            .AllowAnonymous().RequireRateLimiting(RateLimiting.AuthPolicy);
        auth.MapPost("/demo/{role}", DemoSignInAsync).WithName("DemoSignIn")
            .WithSummary("Demo mode only: sign in as the demo admin, developer or reporter without a password.")
            .AllowAnonymous().RequireRateLimiting(RateLimiting.AuthPolicy);
        auth.MapPost("/reset-password", ResetPasswordAsync).WithName("ResetPassword")
            .AllowAnonymous().RequireRateLimiting(RateLimiting.AuthPolicy);
        return auth;
    }

    public static RouteGroupBuilder MapExternalLoginEndpoints(this RouteGroupBuilder auth)
    {
        auth.MapGet("/external/complete", CompleteExternalLoginAsync).WithName("CompleteExternalLogin")
            .WithSummary("Turns the provider's answer into an OpenTicket session, then redirects to the app.")
            .AllowAnonymous().ExcludeFromDescription();
        auth.MapGet("/external/{provider}", StartExternalLoginAsync).WithName("StartExternalLogin")
            .WithSummary("Redirects the browser to GitHub or Google to sign in.")
            .AllowAnonymous().RequireRateLimiting(RateLimiting.AuthPolicy);
        return auth;
    }

    private static async Task<Ok<AuthOptionsResponse>> GetOptionsAsync(
        IAuthenticationSchemeProvider schemes, IAccountRecoveryService recovery, IDemoService demo)
    {
        var providers = new List<string>();
        foreach (var provider in ExternalAuthentication.Providers)
        {
            if (await schemes.GetSchemeAsync(provider) is not null)
            {
                providers.Add(provider);
            }
        }

        return TypedResults.Ok(new AuthOptionsResponse(providers, recovery.IsEnabled && demo.Info is null, demo.Info));
    }

    private static async Task<Results<Ok<SessionResponse>, NotFound, ProblemHttpResult>> DemoSignInAsync(
        string role, IDemoService demo, HttpContext http, CancellationToken ct)
    {
        UserRole? userRole = role.ToUpperInvariant() switch
        {
            "ADMIN" => UserRole.Admin,
            "DEVELOPER" => UserRole.Agent,
            "REPORTER" => UserRole.Customer,
            _ => null,
        };
        if (userRole is null || demo.Info is null)
        {
            return TypedResults.NotFound();
        }

        var result = await demo.SignInAsync(userRole.Value, ct);
        if (!result.IsSuccess)
        {
            return result.Error!.ToProblem();
        }

        var session = result.Value!;
        RefreshCookie.Append(http, session);
        return TypedResults.Ok(new SessionResponse(session.AccessToken, session.AccessTokenExpiresAtUtc, session.User));
    }

    private static async Task<Results<Accepted, ProblemHttpResult>> ForgotPasswordAsync(
        ForgotPasswordRequest request, IAccountRecoveryService recovery, CancellationToken ct)
    {
        var result = await recovery.RequestPasswordResetAsync(request.Email, ct);
        return result.IsSuccess ? TypedResults.Accepted((string?)null) : result.Error!.ToProblem();
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> ResetPasswordAsync(
        ResetPasswordWithTokenRequest request, IAccountRecoveryService recovery, CancellationToken ct)
    {
        var result = await recovery.ResetPasswordAsync(request.UserId!.Value, request.Token, request.NewPassword, ct);
        return result.IsSuccess ? TypedResults.NoContent() : result.Error!.ToProblem();
    }

    private static async Task<Results<ChallengeHttpResult, NotFound>> StartExternalLoginAsync(
        string provider, IAuthenticationSchemeProvider schemes)
    {
        var scheme = ExternalAuthentication.Providers.FirstOrDefault(p => string.Equals(p, provider, StringComparison.OrdinalIgnoreCase));
        if (scheme is null || await schemes.GetSchemeAsync(scheme) is null)
        {
            return TypedResults.NotFound();
        }

        var properties = new AuthenticationProperties { RedirectUri = $"{ExternalAuthentication.PathPrefix}/complete" };
        return TypedResults.Challenge(properties, [scheme]);
    }

    private static async Task<RedirectHttpResult> CompleteExternalLoginAsync(
        HttpContext http, IExternalLoginService external, CancellationToken ct)
    {
        var result = await http.AuthenticateAsync(ExternalAuthentication.CookieScheme);
        await http.SignOutAsync(ExternalAuthentication.CookieScheme);

        var key = result.Principal?.FindFirst(ClaimTypes.NameIdentifier);
        if (!result.Succeeded || key is null || !ExternalAuthentication.Providers.Contains(key.Issuer))
        {
            return TypedResults.Redirect("/login?error=external_failed");
        }

        var principal = result.Principal!;
        var identity = new ExternalIdentity(
            key.Issuer,
            key.Value,
            principal.FindFirstValue(ClaimTypes.Email),
            principal.FindFirstValue(ExternalAuthentication.EmailVerifiedClaim) == "true",
            principal.FindFirstValue(ClaimTypes.Name));

        var session = await external.SignInAsync(identity, ct);
        if (!session.IsSuccess)
        {
            // Only a stable error code goes into the URL; the app maps it to a message.
            return TypedResults.Redirect($"/login?error={Uri.EscapeDataString(session.Error!.Code)}");
        }

        RefreshCookie.Append(http, session.Value!);
        return TypedResults.Redirect("/auth/callback");
    }
}

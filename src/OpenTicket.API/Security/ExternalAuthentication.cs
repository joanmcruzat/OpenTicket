using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;

namespace OpenTicket.API.Security;

/// <summary>Client credentials for one OAuth provider. A provider is enabled only when both values are set.</summary>
internal sealed class ExternalProviderOptions
{
    public string? ClientId { get; init; }

    public string? ClientSecret { get; init; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
}

/// <summary>
/// "Sign in with GitHub / Google" using the framework's OAuth handler (authorization code + PKCE). The provider's
/// answer lands in a short-lived cookie; the /auth/external/complete endpoint turns it into an OpenTicket session.
/// </summary>
internal static class ExternalAuthentication
{
    public const string CookieScheme = "External";
    public const string GitHub = nameof(GitHub);
    public const string Google = nameof(Google);
    public const string EmailVerifiedClaim = "email_verified";
    public const string PathPrefix = "/api/v1/auth/external";

    public static readonly IReadOnlyList<string> Providers = [GitHub, Google];

    public static IServiceCollection AddOpenTicketExternalLogins(this IServiceCollection services, IConfiguration configuration)
    {
        var builder = services.AddAuthentication().AddCookie(CookieScheme, o =>
        {
            o.Cookie.Name = "ot_external";
            o.Cookie.HttpOnly = true;
            o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            o.Cookie.SameSite = SameSiteMode.Lax;
            o.Cookie.Path = PathPrefix;
            o.ExpireTimeSpan = TimeSpan.FromMinutes(5);
            o.Events = new CookieAuthenticationEvents
            {
                OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                },
            };
        });

        if (Bind(configuration, GitHub) is { IsConfigured: true } github)
        {
            builder.AddOAuth(GitHub, o =>
            {
                Configure(o, GitHub, github);
                o.AuthorizationEndpoint = "https://github.com/login/oauth/authorize";
                o.TokenEndpoint = "https://github.com/login/oauth/access_token";
                o.UserInformationEndpoint = "https://api.github.com/user";
                o.Scope.Add("read:user");
                o.Scope.Add("user:email");
                o.Events.OnCreatingTicket = AddGitHubClaimsAsync;
            });
        }

        if (Bind(configuration, Google) is { IsConfigured: true } google)
        {
            builder.AddOAuth(Google, o =>
            {
                Configure(o, Google, google);
                o.AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
                o.TokenEndpoint = "https://oauth2.googleapis.com/token";
                o.UserInformationEndpoint = "https://openidconnect.googleapis.com/v1/userinfo";
                o.Scope.Add("openid");
                o.Scope.Add("email");
                o.Scope.Add("profile");
                o.Events.OnCreatingTicket = AddGoogleClaimsAsync;
            });
        }

        return services;
    }

    private static ExternalProviderOptions? Bind(IConfiguration configuration, string provider) =>
        configuration.GetSection($"Authentication:{provider}").Get<ExternalProviderOptions>();

    private static void Configure(OAuthOptions o, string provider, ExternalProviderOptions settings)
    {
        o.ClientId = settings.ClientId!;
        o.ClientSecret = settings.ClientSecret!;
        o.CallbackPath = $"{PathPrefix}/{provider.ToLowerInvariant()}/callback";
        o.SignInScheme = CookieScheme;
        o.ClaimsIssuer = provider;
        o.UsePkce = true;
        o.SaveTokens = false;
        o.Events.OnRemoteFailure = context =>
        {
            // The user cancelled at the provider, or the state/correlation check failed.
            context.Response.Redirect("/login?error=external_failed");
            context.HandleResponse();
            return Task.CompletedTask;
        };
    }

    private static async Task AddGitHubClaimsAsync(OAuthCreatingTicketContext context)
    {
        using var user = await GetJsonAsync(context, context.Options.UserInformationEndpoint);
        var root = user.RootElement;
        var login = root.GetProperty("login").GetString();
        var name = root.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() : login;

        // GitHub's /user email may be hidden or unverified; /user/emails says which address is primary and verified.
        using var emails = await GetJsonAsync(context, "https://api.github.com/user/emails");
        var primary = emails.RootElement.EnumerateArray()
            .FirstOrDefault(e => e.GetProperty("primary").GetBoolean() && e.GetProperty("verified").GetBoolean());

        AddClaims(context,
            root.GetProperty("id").GetRawText(),
            name,
            primary.ValueKind == JsonValueKind.Object ? primary.GetProperty("email").GetString() : null,
            primary.ValueKind == JsonValueKind.Object);
    }

    private static async Task AddGoogleClaimsAsync(OAuthCreatingTicketContext context)
    {
        using var user = await GetJsonAsync(context, context.Options.UserInformationEndpoint);
        var root = user.RootElement;
        AddClaims(context,
            root.GetProperty("sub").GetString()!,
            root.TryGetProperty("name", out var name) ? name.GetString() : null,
            root.TryGetProperty("email", out var email) ? email.GetString() : null,
            root.TryGetProperty("email_verified", out var verified) && verified.ValueKind == JsonValueKind.True);
    }

    private static void AddClaims(OAuthCreatingTicketContext context, string key, string? name, string? email, bool verified)
    {
        var issuer = context.Options.ClaimsIssuer;
        var identity = context.Identity!;
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, key, ClaimValueTypes.String, issuer));
        if (!string.IsNullOrWhiteSpace(name))
        {
            identity.AddClaim(new Claim(ClaimTypes.Name, name, ClaimValueTypes.String, issuer));
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            identity.AddClaim(new Claim(ClaimTypes.Email, email, ClaimValueTypes.String, issuer));
        }

        identity.AddClaim(new Claim(EmailVerifiedClaim, verified ? "true" : "false", ClaimValueTypes.Boolean, issuer));
    }

    private static async Task<JsonDocument> GetJsonAsync(OAuthCreatingTicketContext context, string url)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.AccessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("OpenTicket", "1.0")); // GitHub requires a User-Agent.
        using var response = await context.Backchannel.SendAsync(request, context.HttpContext.RequestAborted);
        response.EnsureSuccessStatusCode();
        return await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(context.HttpContext.RequestAborted),
            cancellationToken: context.HttpContext.RequestAborted);
    }
}

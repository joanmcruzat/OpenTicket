using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

using OpenTicket.Application.Auth;
using OpenTicket.Domain.Users;
using OpenTicket.Infrastructure.Identity;
using OpenTicket.IntegrationTests.Infrastructure;

namespace OpenTicket.IntegrationTests.Auth;

[Collection(ApiTestGroup.Name)]
public sealed class AuthEndpointTests(OpenTicketApiFactory factory)
{
    private static CancellationToken Ct => TestApi.Ct;

    [Fact]
    public async Task Register_NewEmail_Returns201AsCustomer()
    {
        using var client = factory.CreateApiClient();
        var email = $"new-{Guid.NewGuid():N}@example.test";

        using var response = await client.PostAsJsonAsync(
            "/api/v1/auth/register", new { email, password = "long-enough-pw", displayName = "  Ana  " }, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var profile = await response.ReadJsonAsync<UserProfile>();
        Assert.Equal(UserRole.Customer, profile.Role);
        Assert.Equal("Ana", profile.DisplayName);
        Assert.Equal($"/api/v1/users/{profile.Id}", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Register_RoleFieldInBody_IsIgnoredAndStillCreatesCustomer()
    {
        using var client = factory.CreateApiClient();

        using var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email = $"sneaky-{Guid.NewGuid():N}@example.test",
            password = "long-enough-pw",
            displayName = "Sneaky",
            role = "Admin",
        }, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(UserRole.Customer, (await response.ReadJsonAsync<UserProfile>()).Role);
    }

    [Fact]
    public async Task Register_ExistingEmailInAnyCase_Returns409()
    {
        using var client = factory.CreateApiClient();
        var user = await factory.CreateUserAsync(UserRole.Customer);

        using var response = await client.PostAsJsonAsync("/api/v1/auth/register",
            new { email = user.Email.ToUpperInvariant(), password = "long-enough-pw", displayName = "Dup" }, Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("auth.email_taken", (await response.ReadProblemAsync()).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Register_InvalidFields_Returns400WithFieldErrors()
    {
        using var client = factory.CreateApiClient();

        using var response = await client.PostAsJsonAsync("/api/v1/auth/register",
            new { email = "not-an-email", password = "short", displayName = " " }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.ReadProblemAsync()).GetProperty("errors");
        var fields = errors.EnumerateObject().Select(p => p.Name.ToUpperInvariant()).ToHashSet();
        Assert.Contains("EMAIL", fields);
        Assert.Contains("PASSWORD", fields);
        Assert.Contains("DISPLAYNAME", fields);
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsTokenAndSecureHttpOnlyCookie()
    {
        var user = await factory.CreateUserAsync(UserRole.Agent);
        using var client = factory.CreateApiClient();

        using var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = user.Email, password = user.Password }, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(Ct);
        Assert.Contains("accessToken", body, StringComparison.Ordinal);
        Assert.DoesNotContain("refreshToken", body, StringComparison.OrdinalIgnoreCase);
        var cookie = response.GetSetCookieHeader()!.ToUpperInvariant();
        Assert.Contains("HTTPONLY", cookie, StringComparison.Ordinal);
        Assert.Contains("SECURE", cookie, StringComparison.Ordinal);
        Assert.Contains("SAMESITE=STRICT", cookie, StringComparison.Ordinal);
        Assert.Contains("PATH=/API/V1/AUTH", cookie, StringComparison.Ordinal);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task Login_WrongPasswordOrUnknownEmail_Return401WithSameGenericProblem()
    {
        var user = await factory.CreateUserAsync(UserRole.Customer);
        using var client = factory.CreateApiClient();

        using var wrongPassword = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = user.Email, password = "wrong-password" }, Ct);
        using var unknownEmail = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = "nobody@example.test", password = "whatever-pw" }, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);
        var first = await wrongPassword.ReadProblemAsync();
        var second = await unknownEmail.ReadProblemAsync();
        Assert.Equal("auth.invalid_credentials", first.GetProperty("code").GetString());
        Assert.Equal(first.GetProperty("title").GetString(), second.GetProperty("title").GetString());
        Assert.False(string.IsNullOrEmpty(first.GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task Login_AfterFiveFailures_LocksOutEvenCorrectPassword()
    {
        var user = await factory.CreateUserAsync(UserRole.Customer);
        using var client = factory.CreateApiClient();
        for (var i = 0; i < 5; i++)
        {
            using var failed = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = user.Email, password = "wrong-password" }, Ct);
        }

        using var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = user.Email, password = user.Password }, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_DeactivatedUser_Returns401()
    {
        var user = await factory.CreateUserAsync(UserRole.Customer, isActive: false);
        using var client = factory.CreateApiClient();

        using var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = user.Email, password = user.Password }, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SeedAdmin_CanSignInWithAdminRole()
    {
        using var client = factory.CreateApiClient();

        var (session, _) = await client.LoginAsync(OpenTicketApiFactory.AdminEmail, factory.AdminPassword);

        Assert.Equal(UserRole.Admin, session.User.Role);
    }

    [Fact]
    public async Task Me_WithoutToken_Returns401()
    {
        using var client = factory.CreateApiClient();

        using var response = await client.GetAsync(new Uri("/api/v1/auth/me", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithTamperedToken_Returns401()
    {
        var user = await factory.CreateUserAsync(UserRole.Customer);
        using var client = factory.CreateApiClient();
        var (session, _) = await client.LoginAsync(user.Email, user.Password);
        var tampered = session.AccessToken[..^4] + (session.AccessToken.EndsWith("AAAA", StringComparison.Ordinal) ? "BBBB" : "AAAA");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tampered);

        using var response = await client.GetAsync(new Uri("/api/v1/auth/me", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithToken_ReturnsOwnProfile()
    {
        var user = await factory.CreateUserAsync(UserRole.Agent);
        using var client = await factory.CreateClientForAsync(user);

        using var response = await client.GetAsync(new Uri("/api/v1/auth/me", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var profile = await response.ReadJsonAsync<UserProfile>();
        Assert.Equal(user.Id, profile.Id);
        Assert.Equal(UserRole.Agent, profile.Role);
    }

    [Fact]
    public async Task Refresh_RotatesToken_AndReusingOldTokenRevokesTheNewOne()
    {
        var user = await factory.CreateUserAsync(UserRole.Customer);
        using var client = factory.CreateApiClient();
        var (_, original) = await client.LoginAsync(user.Email, user.Password);

        using var rotated = await client.RefreshAsync(original);
        Assert.Equal(HttpStatusCode.OK, rotated.StatusCode);
        var replacement = rotated.GetRefreshCookie();
        Assert.False(string.IsNullOrEmpty(replacement));
        Assert.NotEqual(original, replacement);

        using var reuse = await client.RefreshAsync(original);
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);

        using var afterReuse = await client.RefreshAsync(replacement!);
        Assert.Equal(HttpStatusCode.Unauthorized, afterReuse.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithoutCookie_Returns401()
    {
        using var client = factory.CreateApiClient();

        using var response = await client.PostAsync(new Uri("/api/v1/auth/refresh", UriKind.Relative), null, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("auth.no_session", (await response.ReadProblemAsync()).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Logout_RevokesRefreshTokenAndClearsCookie()
    {
        var user = await factory.CreateUserAsync(UserRole.Customer);
        using var client = factory.CreateApiClient();
        var (_, refresh) = await client.LoginAsync(user.Email, user.Password);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
        request.Headers.Add("Cookie", $"{TestApi.RefreshCookie}={refresh}");
        using var logout = await client.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Contains("expires=Thu, 01 Jan 1970", logout.GetSetCookieHeader(), StringComparison.OrdinalIgnoreCase);
        using var refreshAfterLogout = await client.RefreshAsync(refresh);
        Assert.Equal(HttpStatusCode.Unauthorized, refreshAfterLogout.StatusCode);
    }

    [Fact]
    public async Task Refresh_AfterUserDeactivated_Returns401()
    {
        var user = await factory.CreateUserAsync(UserRole.Customer);
        using var client = factory.CreateApiClient();
        var (_, refresh) = await client.LoginAsync(user.Email, user.Password);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var entity = await users.FindByIdAsync(user.Id.ToString());
            entity!.IsActive = false;
            await users.UpdateAsync(entity);
        }

        using var response = await client.RefreshAsync(refresh);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AuthEndpoints_OverRateLimit_Return429WithRetryAfter()
    {
        await using var limited = factory.WithWebHostBuilder(b => b.UseSetting("RateLimiting:AuthPermitLimit", "3"));
        using var client = limited.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 4; i++)
        {
            using var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = "x@example.test", password = "whatever-pw" }, Ct);
            statuses.Add(response.StatusCode);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                Assert.NotNull(response.Headers.RetryAfter);
            }
        }

        Assert.Equal([HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.TooManyRequests], statuses);
    }
}

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

using OpenTicket.API.Endpoints;
using OpenTicket.Domain.Users;
using OpenTicket.Infrastructure.Identity;

namespace OpenTicket.IntegrationTests.Infrastructure;

public sealed record TestUser(Guid Id, string Email, string Password, UserRole Role);

internal static class TestApi
{
    public const string RefreshCookie = "ot_refresh";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Creates a user directly through Identity (bypassing the public sign-up, which only makes Customers).</summary>
    public static async Task<TestUser> CreateUserAsync(this OpenTicketApiFactory factory, UserRole role, bool isActive = true)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var email = $"{role.ToString().ToLowerInvariant()}-{Guid.NewGuid():N}@example.test";
        var password = $"Pw-{Guid.NewGuid():N}";
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            DisplayName = $"Test {role}",
            IsActive = isActive,
            CreatedAtUtc = DateTime.UtcNow,
        };
        AssertSucceeded(await users.CreateAsync(user, password));
        AssertSucceeded(await users.AddToRoleAsync(user, role.ToString()));
        return new TestUser(user.Id, email, password, role);
    }

    public static async Task<HttpClient> CreateClientForAsync(this OpenTicketApiFactory factory, TestUser user)
    {
        var client = factory.CreateApiClient();
        var (session, _) = await client.LoginAsync(user.Email, user.Password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        return client;
    }

    public static async Task<HttpClient> CreateClientForNewUserAsync(this OpenTicketApiFactory factory, UserRole role) =>
        await factory.CreateClientForAsync(await factory.CreateUserAsync(role));

    public static async Task<(SessionResponse Session, string RefreshToken)> LoginAsync(
        this HttpClient client, string email, string password)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password }, Ct);
        response.EnsureSuccessStatusCode();
        var session = await response.ReadJsonAsync<SessionResponse>();
        return (session, response.GetRefreshCookie() ?? throw new InvalidOperationException("No refresh cookie."));
    }

    public static async Task<HttpResponseMessage> RefreshAsync(this HttpClient client, string refreshToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        request.Headers.Add("Cookie", $"{RefreshCookie}={refreshToken}");
        return await client.SendAsync(request, Ct);
    }

    public static string? GetRefreshCookie(this HttpResponseMessage response) =>
        response.GetSetCookieHeader()?.Split(';')[0].Split('=', 2)[1] is { Length: > 0 } value ? value : null;

    public static string? GetSetCookieHeader(this HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.FirstOrDefault(v => v.StartsWith($"{RefreshCookie}=", StringComparison.Ordinal))
            : null;

    public static async Task<T> ReadJsonAsync<T>(this HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<T>(Json, Ct) ?? throw new InvalidOperationException("Empty body.");

    public static async Task<JsonElement> ReadProblemAsync(this HttpResponseMessage response)
    {
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        return await response.ReadJsonAsync<JsonElement>();
    }

    private static void AssertSucceeded(IdentityResult result) => Assert.True(result.Succeeded, result.ToString());
}

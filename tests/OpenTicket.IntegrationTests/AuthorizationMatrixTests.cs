using System.Net;
using System.Text;

using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

using OpenTicket.Domain.Users;
using OpenTicket.IntegrationTests.Infrastructure;

namespace OpenTicket.IntegrationTests;

/// <summary>
/// Table-driven checks of docs/architecture.md §6 at the HTTP boundary. Authorization runs before
/// validation, so placeholder IDs and empty bodies are enough to observe 401/403.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class AuthorizationMatrixTests(OpenTicketApiFactory factory)
{
    private static readonly string Id = Guid.NewGuid().ToString();

    /// <summary>Endpoints that intentionally allow anonymous callers.</summary>
    private static readonly HashSet<string> PublicRoutes =
    [
        "POST /api/v1/auth/register",
        "POST /api/v1/auth/login",
        "POST /api/v1/auth/refresh",
        "POST /api/v1/auth/logout",
        "GET /api/v1/auth/options",
        "POST /api/v1/auth/forgot-password",
        "POST /api/v1/auth/reset-password",
        "GET /api/v1/auth/external/{provider}",
        "GET /api/v1/auth/external/complete",
        "POST /api/v1/auth/demo/{role}",
        "GET /health/live",
        "GET /health/ready",
    ];

    public static TheoryData<string, string> AdminOnlyEndpoints => new()
    {
        { "GET", "/api/v1/users" },
        { "GET", "/api/v1/users/agents" },
        { "GET", $"/api/v1/users/{Id}" },
        { "POST", "/api/v1/users" },
        { "PUT", $"/api/v1/users/{Id}/role" },
        { "POST", $"/api/v1/users/{Id}/activate" },
        { "POST", $"/api/v1/users/{Id}/deactivate" },
        { "POST", $"/api/v1/users/{Id}/reset-password" },
        { "POST", "/api/v1/categories" },
        { "PUT", $"/api/v1/categories/{Id}" },
        { "POST", $"/api/v1/categories/{Id}/activate" },
        { "POST", $"/api/v1/categories/{Id}/deactivate" },
    };

    public static TheoryData<string, string> StaffOnlyEndpoints => new()
    {
        { "PUT", $"/api/v1/tickets/{Id}/assignee" },
        { "DELETE", $"/api/v1/tickets/{Id}/assignee?version=1" },
        { "PUT", $"/api/v1/tickets/{Id}/sprint" },
        { "GET", "/api/v1/sprints/plan" },
        { "POST", "/api/v1/sprints" },
        { "PUT", $"/api/v1/sprints/{Id}" },
        { "POST", $"/api/v1/sprints/{Id}/start" },
        { "POST", $"/api/v1/sprints/{Id}/complete" },
        { "DELETE", $"/api/v1/sprints/{Id}" },
    };

    [Fact]
    public async Task EveryNonPublicEndpoint_RejectsAnonymousCallersWith401()
    {
        var routes = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .SelectMany(e => (e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? []).Select(m => (Method: m, e.RoutePattern.RawText!)))
            .Where(r => r.Item2.StartsWith("/api/", StringComparison.Ordinal))
            .Select(r => (r.Method, Path: "/" + r.Item2.TrimStart('/')))
            .Where(r => !PublicRoutes.Contains($"{r.Method} {r.Path}") && !r.Path.Contains("{**path}", StringComparison.Ordinal))
            .ToList();
        Assert.True(routes.Count >= 25, $"Expected the full API surface, found {routes.Count} routes.");
        using var client = factory.CreateApiClient();

        var failures = new List<string>();
        foreach (var (method, pattern) in routes)
        {
            var path = pattern.Replace("{ticketId:guid}", Id, StringComparison.Ordinal)
                .Replace("{userId:guid}", Id, StringComparison.Ordinal)
                .Replace("{categoryId:guid}", Id, StringComparison.Ordinal)
                .Replace("{sprintId:guid}", Id, StringComparison.Ordinal);
            using var response = await SendAsync(client, method, path);
            if (response.StatusCode != HttpStatusCode.Unauthorized)
            {
                failures.Add($"{method} {pattern} → {(int)response.StatusCode}");
            }
        }

        Assert.Empty(failures);
    }

    [Theory]
    [MemberData(nameof(AdminOnlyEndpoints))]
    public async Task AdminOnlyEndpoints_ReturnForbiddenToAgentsAndCustomers(string method, string path)
    {
        foreach (var role in new[] { UserRole.Agent, UserRole.Customer })
        {
            using var client = await factory.CreateClientForNewUserAsync(role);

            using var response = await SendAsync(client, method, path);

            Assert.True(response.StatusCode == HttpStatusCode.Forbidden, $"{role}: {method} {path} returned {(int)response.StatusCode}");
        }
    }

    [Theory]
    [MemberData(nameof(StaffOnlyEndpoints))]
    public async Task StaffOnlyEndpoints_ReturnForbiddenToReporters(string method, string path)
    {
        using var client = await factory.CreateClientForNewUserAsync(UserRole.Customer);

        using var response = await SendAsync(client, method, path);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, string method, string path)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method is "POST" or "PUT" or "PATCH")
        {
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        }

        return client.SendAsync(request, TestApi.Ct);
    }
}

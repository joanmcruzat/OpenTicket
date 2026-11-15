using System.Net;

using OpenTicket.IntegrationTests.Infrastructure;

namespace OpenTicket.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public sealed class HealthEndpointTests(OpenTicketApiFactory factory)
{
    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task Health_ReturnsHealthyWithoutAuthentication(string path)
    {
        using var client = factory.CreateApiClient();

        using var response = await client.GetAsync(new Uri(path, UriKind.Relative), TestApi.Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(TestApi.Ct));
    }
}

[Collection(ApiTestGroup.Name)]
public sealed class RoutingTests(OpenTicketApiFactory factory)
{
    [Fact]
    public async Task UnknownApiRoute_ReturnsProblem404_NotTheSpaShell()
    {
        using var client = factory.CreateApiClient();

        using var response = await client.GetAsync(new Uri("/api/v1/does-not-exist", UriKind.Relative), TestApi.Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task ApiResponses_CarrySecurityHeaders()
    {
        using var client = factory.CreateApiClient();

        using var response = await client.GetAsync(new Uri("/api/v1/auth/me", UriKind.Relative), TestApi.Ct);

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Contains("frame-ancestors 'none'", response.Headers.GetValues("Content-Security-Policy").Single(), StringComparison.Ordinal);
        Assert.False(response.Headers.Contains("Server"));
    }
}

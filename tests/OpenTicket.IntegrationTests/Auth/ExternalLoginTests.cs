using System.Net;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;

using OpenTicket.Application.Auth;
using OpenTicket.Domain.Users;
using OpenTicket.Infrastructure.Identity;
using OpenTicket.IntegrationTests.Infrastructure;

namespace OpenTicket.IntegrationTests.Auth;

[Collection(ApiTestGroup.Name)]
public sealed class ExternalLoginTests(OpenTicketApiFactory factory)
{
    private static CancellationToken Ct => TestApi.Ct;

    [Fact]
    public async Task Start_ConfiguredProvider_RedirectsToGitHubWithPkceAndCallback()
    {
        using var client = factory.CreateApiClient();

        using var response = await client.GetAsync(new Uri("/api/v1/auth/external/github", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!;
        Assert.Equal("github.com", location.Host);
        var query = QueryHelpers.ParseQuery(location.Query);
        Assert.Equal("test-client-id", query["client_id"]);
        Assert.Equal("https://localhost/api/v1/auth/external/github/callback", query["redirect_uri"]);
        Assert.Equal("S256", query["code_challenge_method"]);
        Assert.False(string.IsNullOrEmpty(query["state"]));
    }

    [Theory]
    [InlineData("google")]
    [InlineData("myspace")]
    public async Task Start_UnconfiguredOrUnknownProvider_Returns404(string provider)
    {
        using var client = factory.CreateApiClient();

        using var response = await client.GetAsync(new Uri($"/api/v1/auth/external/{provider}", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Complete_WithoutProviderAnswer_RedirectsToLoginWithError()
    {
        using var client = factory.CreateApiClient();

        using var response = await client.GetAsync(new Uri("/api/v1/auth/external/complete", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/login?error=external_failed", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task SignIn_NewVerifiedIdentity_CreatesReporterOnce_ThenFindsItByLogin()
    {
        var email = $"octo-{Guid.NewGuid():N}@example.test";
        var identity = new ExternalIdentity("GitHub", Guid.NewGuid().ToString("N"), email, true, "Octo Cat");

        var first = await SignInAsync(identity);
        var second = await SignInAsync(identity with { Email = "changed@example.test" });

        Assert.True(first.IsSuccess);
        Assert.Equal(UserRole.Customer, first.Value!.User.Role);
        Assert.Equal("Octo Cat", first.Value.User.DisplayName);
        Assert.Equal(first.Value.User.Id, second.Value!.User.Id);
        Assert.False(string.IsNullOrEmpty(first.Value.RefreshToken));
    }

    [Fact]
    public async Task SignIn_VerifiedEmailOfExistingAccount_LinksIt_AndPasswordStillWorks()
    {
        var user = await factory.CreateUserAsync(UserRole.Agent);

        var result = await SignInAsync(new ExternalIdentity("Google", Guid.NewGuid().ToString("N"), user.Email.ToUpperInvariant(), true, null));

        Assert.Equal(user.Id, result.Value!.User.Id);
        Assert.Equal(UserRole.Agent, result.Value.User.Role);
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var stored = await users.FindByIdAsync(user.Id.ToString());
        Assert.Single(await users.GetLoginsAsync(stored!));
        using var client = factory.CreateApiClient();
        await client.LoginAsync(user.Email, user.Password);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("someone@example.test", false)]
    public async Task SignIn_WithoutVerifiedEmail_IsRejected(string? email, bool verified)
    {
        var result = await SignInAsync(new ExternalIdentity("GitHub", Guid.NewGuid().ToString("N"), email, verified, "x"));

        Assert.Equal("auth.external_email_unverified", result.Error?.Code);
    }

    [Fact]
    public async Task SignIn_DeactivatedAccount_IsRejected()
    {
        var user = await factory.CreateUserAsync(UserRole.Customer, isActive: false);

        var result = await SignInAsync(new ExternalIdentity("GitHub", Guid.NewGuid().ToString("N"), user.Email, true, null));

        Assert.Equal("auth.account_disabled", result.Error?.Code);
    }

    private async Task<OpenTicket.Domain.Common.Result<AuthSession>> SignInAsync(ExternalIdentity identity)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IExternalLoginService>().SignInAsync(identity, Ct);
    }
}

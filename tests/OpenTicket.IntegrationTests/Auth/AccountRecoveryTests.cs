using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;

using Microsoft.AspNetCore.WebUtilities;

using OpenTicket.Domain.Users;
using OpenTicket.IntegrationTests.Infrastructure;

namespace OpenTicket.IntegrationTests.Auth;

[Collection(ApiTestGroup.Name)]
public sealed partial class AccountRecoveryTests(OpenTicketApiFactory factory)
{
    private static CancellationToken Ct => TestApi.Ct;

    [Fact]
    public async Task Options_ListConfiguredProvidersAndPasswordReset()
    {
        using var client = factory.CreateApiClient();

        using var response = await client.GetAsync(new Uri("/api/v1/auth/options", UriKind.Relative), Ct);
        var options = await response.ReadJsonAsync<OptionsDto>();

        Assert.Equal(["GitHub"], options.ExternalProviders);
        Assert.True(options.PasswordReset);
    }

    [Fact]
    public async Task ForgotPassword_UnknownEmail_Returns202AndSendsNothing()
    {
        using var client = factory.CreateApiClient();
        var email = $"nobody-{Guid.NewGuid():N}@example.test";

        using var response = await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email }, Ct);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.DoesNotContain(factory.Emails.Sent, m => m.To == email);
    }

    [Fact]
    public async Task ResetPassword_WithEmailedLink_SetsNewPassword_AndEndsOldSessions()
    {
        var user = await factory.CreateUserAsync(UserRole.Customer);
        using var client = factory.CreateApiClient();
        var (_, oldRefresh) = await client.LoginAsync(user.Email, user.Password);

        var (userId, token) = await RequestResetLinkAsync(client, user.Email);
        using var reset = await client.PostAsJsonAsync("/api/v1/auth/reset-password",
            new { userId, token, newPassword = "brand-new-password" }, Ct);

        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        Assert.Equal(user.Id, userId);
        await client.LoginAsync(user.Email, "brand-new-password");
        using var oldLogin = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = user.Email, password = user.Password }, Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);
        using var oldSession = await client.RefreshAsync(oldRefresh);
        Assert.Equal(HttpStatusCode.Unauthorized, oldSession.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_LinkIsSingleUse_AndForgedTokenIsRejected()
    {
        var user = await factory.CreateUserAsync(UserRole.Agent);
        using var client = factory.CreateApiClient();
        var (userId, token) = await RequestResetLinkAsync(client, user.Email);

        using var forged = await client.PostAsJsonAsync("/api/v1/auth/reset-password",
            new { userId, token = "not-a-real-token", newPassword = "another-password" }, Ct);
        using var first = await client.PostAsJsonAsync("/api/v1/auth/reset-password",
            new { userId, token, newPassword = "another-password" }, Ct);
        using var replay = await client.PostAsJsonAsync("/api/v1/auth/reset-password",
            new { userId, token, newPassword = "yet-another-password" }, Ct);

        Assert.Equal("auth.invalid_reset_link", await forged.ProblemCodeAsync());
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
        Assert.Equal("auth.invalid_reset_link", await replay.ProblemCodeAsync());
    }

    [Fact]
    public async Task ResetPassword_UnlocksLockedOutAccount()
    {
        var user = await factory.CreateUserAsync(UserRole.Customer);
        using var client = factory.CreateApiClient();
        for (var i = 0; i < 5; i++)
        {
            using var _ = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = user.Email, password = "wrong-password" }, Ct);
        }

        var (userId, token) = await RequestResetLinkAsync(client, user.Email);
        using var reset = await client.PostAsJsonAsync("/api/v1/auth/reset-password",
            new { userId, token, newPassword = "unlocked-password" }, Ct);

        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        await client.LoginAsync(user.Email, "unlocked-password");
    }

    [Fact]
    public async Task ResetPassword_WeakPassword_Returns400OnNewPassword()
    {
        var user = await factory.CreateUserAsync(UserRole.Customer);
        using var client = factory.CreateApiClient();
        var (userId, token) = await RequestResetLinkAsync(client, user.Email);

        using var response = await client.PostAsJsonAsync("/api/v1/auth/reset-password", new { userId, token, newPassword = "short" }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var fields = (await response.ReadProblemAsync()).GetProperty("errors").EnumerateObject().Select(p => p.Name);
        Assert.Contains(fields, f => string.Equals(f, "newPassword", StringComparison.OrdinalIgnoreCase));
    }

    private async Task<(Guid UserId, string Token)> RequestResetLinkAsync(HttpClient client, string email)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email }, Ct);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var message = factory.Emails.Sent.Last(m => m.To == email);
        var link = new Uri(LinkPattern().Match(message.TextBody).Value);
        Assert.Equal("openticket.test", link.Host);
        Assert.Equal("/reset-password", link.AbsolutePath);
        var query = QueryHelpers.ParseQuery(link.Query);
        return (Guid.Parse(query["uid"]!), query["token"]!);
    }

    [GeneratedRegex(@"https://openticket\.test/reset-password\S+")]
    private static partial Regex LinkPattern();

    private sealed record OptionsDto(List<string> ExternalProviders, bool PasswordReset);
}

public sealed class AppOptionsTests
{
    [Theory]
    [InlineData("https://tickets.example.com", true)]
    [InlineData("http://localhost:4200", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("tickets.example.com", false)]
    [InlineData("javascript:alert(1)", false)]
    public void HasValidPublicUrl_AcceptsOnlyAbsoluteHttpUrls(string? url, bool expected) =>
        Assert.Equal(expected, new OpenTicket.Infrastructure.Identity.AppOptions { PublicUrl = url }.HasValidPublicUrl);
}

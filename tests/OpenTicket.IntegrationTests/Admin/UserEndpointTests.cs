using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using OpenTicket.Application.Common;
using OpenTicket.Application.Users;
using OpenTicket.Domain.Users;
using OpenTicket.IntegrationTests.Infrastructure;

namespace OpenTicket.IntegrationTests.Admin;

[Collection(ApiTestGroup.Name)]
public sealed class UserEndpointTests(OpenTicketApiFactory factory)
{
    private static CancellationToken Ct => TestApi.Ct;

    [Theory]
    [InlineData(UserRole.Agent)]
    [InlineData(UserRole.Customer)]
    public async Task UserEndpoints_ForNonAdmins_Return403(UserRole role)
    {
        using var client = await factory.CreateClientForNewUserAsync(role);

        using var response = await client.GetAsync(new Uri("/api/v1/users", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_Agent_CanSignInAndAppearsInAgentPicker()
    {
        using var admin = await factory.CreateClientForNewUserAsync(UserRole.Admin);
        var email = $"agent-{Guid.NewGuid():N}@example.test";

        using var created = await admin.PostAsJsonAsync("/api/v1/users",
            new { email, displayName = "Dana Agent", role = "Agent", temporaryPassword = "temporary-pw-1" }, Ct);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var user = await created.ReadJsonAsync<UserListItem>();
        Assert.Equal(UserRole.Agent, user.Role);
        using var client = factory.CreateApiClient();
        var (session, _) = await client.LoginAsync(email, "temporary-pw-1");
        Assert.Equal(UserRole.Agent, session.User.Role);

        using var agents = await admin.GetAsync(new Uri("/api/v1/users/agents", UriKind.Relative), Ct);
        var option = Assert.Single(await agents.ReadJsonAsync<List<AgentOption>>(), a => a.Id == user.Id);
        Assert.Equal(0, option.OpenTicketCount);
    }

    [Fact]
    public async Task Create_DuplicateEmail_Returns409_AndShortPassword400()
    {
        using var admin = await factory.CreateClientForNewUserAsync(UserRole.Admin);
        var existing = await factory.CreateUserAsync(UserRole.Customer);

        using var duplicate = await admin.PostAsJsonAsync("/api/v1/users",
            new { email = existing.Email, displayName = "Dup", role = "Agent", temporaryPassword = "temporary-pw-1" }, Ct);
        using var weak = await admin.PostAsJsonAsync("/api/v1/users",
            new { email = $"x-{Guid.NewGuid():N}@example.test", displayName = "Weak", role = "Agent", temporaryPassword = "short" }, Ct);

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);
    }

    [Fact]
    public async Task List_FiltersByRoleAndSearch()
    {
        using var admin = await factory.CreateClientForNewUserAsync(UserRole.Admin);
        var agent = await factory.CreateUserAsync(UserRole.Agent);

        using var response = await admin.GetAsync(new Uri($"/api/v1/users?role=Agent&search={agent.Email[..12]}", UriKind.Relative), Ct);

        var page = await response.ReadJsonAsync<PagedResult<UserListItem>>();
        var found = Assert.Single(page.Items);
        Assert.Equal(agent.Id, found.Id);
    }

    [Fact]
    public async Task Deactivate_RejectsTheUsersExistingTokenImmediately()
    {
        using var admin = await factory.CreateClientForNewUserAsync(UserRole.Admin);
        var customerUser = await factory.CreateUserAsync(UserRole.Customer);
        using var customer = await factory.CreateClientForAsync(customerUser);
        Assert.Equal(HttpStatusCode.OK, (await customer.GetAsync(new Uri("/api/v1/auth/me", UriKind.Relative), Ct)).StatusCode);

        using var deactivated = await admin.PostAsync(new Uri($"/api/v1/users/{customerUser.Id}/deactivate", UriKind.Relative), null, Ct);
        Assert.Equal(HttpStatusCode.OK, deactivated.StatusCode);

        using var afterward = await customer.GetAsync(new Uri("/api/v1/auth/me", UriKind.Relative), Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, afterward.StatusCode);

        using var reactivated = await admin.PostAsync(new Uri($"/api/v1/users/{customerUser.Id}/activate", UriKind.Relative), null, Ct);
        Assert.True((await reactivated.ReadJsonAsync<UserListItem>()).IsActive);
    }

    [Fact]
    public async Task ChangeRole_RejectsOldToken_AndAgentWithOpenTicketsIs409()
    {
        var category = await factory.CreateCategoryAsync();
        using var admin = await factory.CreateClientForNewUserAsync(UserRole.Admin);
        var customerUser = await factory.CreateUserAsync(UserRole.Customer);
        using var customer = await factory.CreateClientForAsync(customerUser);

        using var promoted = await admin.PutAsJsonAsync($"/api/v1/users/{customerUser.Id}/role", new { role = "Agent" }, Ct);
        Assert.Equal(UserRole.Agent, (await promoted.ReadJsonAsync<UserListItem>()).Role);
        using var oldToken = await customer.GetAsync(new Uri("/api/v1/auth/me", UriKind.Relative), Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, oldToken.StatusCode);

        using var ticketOwner = await factory.CreateClientForNewUserAsync(UserRole.Customer);
        await admin.AssignOkAsync(await ticketOwner.CreateTicketAsync(category.Id), customerUser.Id);
        using var demote = await admin.PutAsJsonAsync($"/api/v1/users/{customerUser.Id}/role", new { role = "Customer" }, Ct);
        Assert.Equal(HttpStatusCode.Conflict, demote.StatusCode);
        Assert.Equal("user.has_assignments", await demote.ProblemCodeAsync());
    }

    [Fact]
    public async Task Admin_CannotDemoteOrDeactivateThemselves()
    {
        var adminUser = await factory.CreateUserAsync(UserRole.Admin);
        using var admin = await factory.CreateClientForAsync(adminUser);

        using var demote = await admin.PutAsJsonAsync($"/api/v1/users/{adminUser.Id}/role", new { role = "Customer" }, Ct);
        using var deactivate = await admin.PostAsync(new Uri($"/api/v1/users/{adminUser.Id}/deactivate", UriKind.Relative), null, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, demote.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, deactivate.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_ReplacesPasswordAndUnlocksAccount()
    {
        using var admin = await factory.CreateClientForNewUserAsync(UserRole.Admin);
        var user = await factory.CreateUserAsync(UserRole.Customer);
        using var anonymous = factory.CreateApiClient();
        for (var i = 0; i < 5; i++)
        {
            using var _ = await anonymous.PostAsJsonAsync("/api/v1/auth/login", new { email = user.Email, password = "wrong-password" }, Ct);
        }

        using var reset = await admin.PostAsJsonAsync($"/api/v1/users/{user.Id}/reset-password", new { temporaryPassword = "brand-new-temp-pw" }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);

        using var oldPassword = await anonymous.PostAsJsonAsync("/api/v1/auth/login", new { email = user.Email, password = user.Password }, Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, oldPassword.StatusCode);
        var (session, _) = await anonymous.LoginAsync(user.Email, "brand-new-temp-pw");
        Assert.Equal(user.Id, session.User.Id);
    }

    [Fact]
    public async Task ChangePassword_WrongCurrent400_ThenSuccessEndsOtherSessions()
    {
        var user = await factory.CreateUserAsync(UserRole.Customer);
        using var client = factory.CreateApiClient();
        var (session, refresh) = await client.LoginAsync(user.Email, user.Password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);

        using var wrong = await client.PostAsJsonAsync("/api/v1/auth/change-password",
            new { currentPassword = "not-my-password", newPassword = "a-new-password-1" }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        Assert.Equal("auth.wrong_password", await wrong.ProblemCodeAsync());

        using var changed = await client.PostAsJsonAsync("/api/v1/auth/change-password",
            new { currentPassword = user.Password, newPassword = "a-new-password-1" }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);

        using var oldRefresh = await client.RefreshAsync(refresh);
        Assert.Equal(HttpStatusCode.Unauthorized, oldRefresh.StatusCode);
        await client.LoginAsync(user.Email, "a-new-password-1");
    }
}

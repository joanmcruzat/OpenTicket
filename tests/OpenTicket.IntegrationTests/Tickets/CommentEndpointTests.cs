using System.Net;
using System.Net.Http.Json;

using OpenTicket.Application.Comments;
using OpenTicket.Application.Common;
using OpenTicket.Domain.Users;
using OpenTicket.IntegrationTests.Infrastructure;

namespace OpenTicket.IntegrationTests.Tickets;

[Collection(ApiTestGroup.Name)]
public sealed class CommentEndpointTests(OpenTicketApiFactory factory)
{
    private static CancellationToken Ct => TestApi.Ct;

    [Fact]
    public async Task Conversation_OwnerAndAssignedAgent_ListedOldestFirstWithAuthors()
    {
        var category = await factory.CreateCategoryAsync();
        var agentUser = await factory.CreateUserAsync(UserRole.Agent);
        using var agent = await factory.CreateClientForAsync(agentUser);
        using var customer = await factory.CreateClientForNewUserAsync(UserRole.Customer);
        using var admin = await factory.CreateClientForNewUserAsync(UserRole.Admin);
        var ticket = await admin.AssignOkAsync(await customer.CreateTicketAsync(category.Id), agentUser.Id);

        using var first = await customer.PostAsJsonAsync($"/api/v1/tickets/{ticket.Id}/comments", new { body = "Any update?" }, Ct);
        using var second = await agent.PostAsJsonAsync($"/api/v1/tickets/{ticket.Id}/comments", new { body = "  On it.  " }, Ct);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);

        using var list = await customer.GetAsync(new Uri($"/api/v1/tickets/{ticket.Id}/comments", UriKind.Relative), Ct);
        var page = await list.ReadJsonAsync<PagedResult<CommentView>>();

        Assert.Equal(["Any update?", "On it."], page.Items.Select(c => c.Body));
        Assert.Equal(UserRole.Customer, page.Items[0].AuthorRole);
        Assert.Equal("Test Agent", page.Items[1].AuthorName);
        Assert.Equal(UserRole.Agent, page.Items[1].AuthorRole);
    }

    [Fact]
    public async Task Comments_OnSomeoneElsesTicket_Return404()
    {
        var category = await factory.CreateCategoryAsync();
        using var owner = await factory.CreateClientForNewUserAsync(UserRole.Customer);
        using var stranger = await factory.CreateClientForNewUserAsync(UserRole.Customer);
        var ticket = await owner.CreateTicketAsync(category.Id);

        using var read = await stranger.GetAsync(new Uri($"/api/v1/tickets/{ticket.Id}/comments", UriKind.Relative), Ct);
        using var write = await stranger.PostAsJsonAsync($"/api/v1/tickets/{ticket.Id}/comments", new { body = "hi" }, Ct);

        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, write.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AddComment_Blank_Returns400(string body)
    {
        var category = await factory.CreateCategoryAsync();
        using var owner = await factory.CreateClientForNewUserAsync(UserRole.Customer);
        var ticket = await owner.CreateTicketAsync(category.Id);

        using var response = await owner.PostAsJsonAsync($"/api/v1/tickets/{ticket.Id}/comments", new { body }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

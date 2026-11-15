using OpenTicket.Application.Dashboard;
using OpenTicket.Domain.Tickets;
using OpenTicket.Domain.Users;
using OpenTicket.IntegrationTests.Infrastructure;

namespace OpenTicket.IntegrationTests.Admin;

[Collection(ApiTestGroup.Name)]
public sealed class DashboardEndpointTests(OpenTicketApiFactory factory)
{
    [Fact]
    public async Task Summary_Reporter_CountsOwnTicketsOnly()
    {
        var category = await factory.CreateCategoryAsync();
        using var customer = await factory.CreateClientForNewUserAsync(UserRole.Customer);
        using var other = await factory.CreateClientForNewUserAsync(UserRole.Customer);
        await other.CreateTicketAsync(category.Id);
        await customer.CreateTicketAsync(category.Id);
        await customer.ChangeStatusOkAsync(await customer.CreateTicketAsync(category.Id), TicketStatus.WontDo, "Duplicate");

        var summary = await customer.GetSummaryAsync();

        Assert.Equal(2, summary.Total);
        Assert.Equal(1, Count(summary, TicketStatus.Backlog));
        Assert.Equal(1, Count(summary, TicketStatus.WontDo));
        Assert.Equal(Enum.GetValues<TicketStatus>(), summary.ByStatus.Select(c => c.Status));
        Assert.Null(summary.AssignedToMe);
        Assert.Null(summary.Unassigned);
    }

    [Fact]
    public async Task Summary_Developer_CountsAssignedToMe()
    {
        var category = await factory.CreateCategoryAsync();
        var agentUser = await factory.CreateUserAsync(UserRole.Agent);
        using var agent = await factory.CreateClientForAsync(agentUser);
        using var customer = await factory.CreateClientForNewUserAsync(UserRole.Customer);
        using var admin = await factory.CreateClientForNewUserAsync(UserRole.Admin);
        await admin.AssignOkAsync(await customer.CreateTicketAsync(category.Id), agentUser.Id);
        var working = await admin.AssignOkAsync(await customer.CreateTicketAsync(category.Id), agentUser.Id);
        await agent.ChangeStatusOkAsync(working, TicketStatus.InProgress);
        await customer.CreateTicketAsync(category.Id);

        var summary = await agent.GetSummaryAsync();

        Assert.Equal(2, summary.AssignedToMe);
        Assert.NotNull(summary.Unassigned);
        Assert.True(summary.Total >= 3);
    }

    [Fact]
    public async Task Summary_Admin_SeesGlobalTotals()
    {
        var category = await factory.CreateCategoryAsync();
        using var customer = await factory.CreateClientForNewUserAsync(UserRole.Customer);
        using var admin = await factory.CreateClientForNewUserAsync(UserRole.Admin);
        var before = await admin.GetSummaryAsync();
        await customer.CreateTicketAsync(category.Id);

        var after = await admin.GetSummaryAsync();

        Assert.True(after.Total >= before.Total + 1);
        Assert.Equal(after.Total, after.ByStatus.Sum(c => c.Count));
        Assert.Null(after.AssignedToMe);
        Assert.True(after.Unassigned >= 1);
    }

    private static int Count(DashboardSummary summary, TicketStatus status) =>
        summary.ByStatus.Single(c => c.Status == status).Count;
}

internal static class DashboardApi
{
    public static async Task<DashboardSummary> GetSummaryAsync(this HttpClient client)
    {
        using var response = await client.GetAsync(new Uri("/api/v1/dashboard/summary", UriKind.Relative), TestApi.Ct);
        response.EnsureSuccessStatusCode();
        return await response.ReadJsonAsync<DashboardSummary>();
    }
}

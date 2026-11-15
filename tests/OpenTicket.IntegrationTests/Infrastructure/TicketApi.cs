using System.Net.Http.Json;

using Microsoft.Extensions.DependencyInjection;

using OpenTicket.Application.Tickets;
using OpenTicket.Domain.Categories;
using OpenTicket.Domain.Tickets;
using OpenTicket.Infrastructure.Persistence;

namespace OpenTicket.IntegrationTests.Infrastructure;

internal static class TicketApi
{
    public static async Task<TicketCategory> CreateCategoryAsync(this OpenTicketApiFactory factory, bool isActive = true)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OpenTicketDbContext>();
        var category = new TicketCategory($"Category {Guid.NewGuid():N}", null, DateTime.UtcNow);
        if (!isActive)
        {
            category.Deactivate(DateTime.UtcNow);
        }

        db.TicketCategories.Add(category);
        await db.SaveChangesAsync(TestApi.Ct);
        return category;
    }

    public static async Task<TicketDetail> CreateTicketAsync(
        this HttpClient client,
        Guid categoryId,
        string title = "Login button unresponsive",
        TicketPriority priority = TicketPriority.Medium,
        TicketType type = TicketType.Bug)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/tickets",
            new { title, description = "Clicking it does nothing.", type, categoryId, priority }, TestApi.Json, TestApi.Ct);
        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);
        return await response.ReadJsonAsync<TicketDetail>();
    }

    public static async Task<HttpResponseMessage> ChangeStatusAsync(
        this HttpClient client, TicketDetail ticket, TicketStatus status, string? reason = null) =>
        await client.PostAsJsonAsync($"/api/v1/tickets/{ticket.Id}/status",
            new { status, reason, version = ticket.Version }, TestApi.Json, TestApi.Ct);

    public static async Task<TicketDetail> ChangeStatusOkAsync(
        this HttpClient client, TicketDetail ticket, TicketStatus status, string? reason = null)
    {
        using var response = await client.ChangeStatusAsync(ticket, status, reason);
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        return await response.ReadJsonAsync<TicketDetail>();
    }

    public static async Task<HttpResponseMessage> AssignAsync(this HttpClient client, TicketDetail ticket, Guid assigneeId) =>
        await client.PutAsJsonAsync($"/api/v1/tickets/{ticket.Id}/assignee",
            new { assigneeId, version = ticket.Version }, TestApi.Ct);

    public static async Task<TicketDetail> AssignOkAsync(this HttpClient client, TicketDetail ticket, Guid assigneeId)
    {
        using var response = await client.AssignAsync(ticket, assigneeId);
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        return await response.ReadJsonAsync<TicketDetail>();
    }

    public static async Task<TicketDetail> GetTicketAsync(this HttpClient client, Guid ticketId)
    {
        using var response = await client.GetAsync(new Uri($"/api/v1/tickets/{ticketId}", UriKind.Relative), TestApi.Ct);
        response.EnsureSuccessStatusCode();
        return await response.ReadJsonAsync<TicketDetail>();
    }

    public static async Task<string?> ProblemCodeAsync(this HttpResponseMessage response) =>
        (await response.ReadProblemAsync()).TryGetProperty("code", out var code) ? code.GetString() : null;
}

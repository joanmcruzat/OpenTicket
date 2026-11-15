using System.Net;
using System.Net.Http.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using OpenTicket.Application.Sprints;
using OpenTicket.Application.Tickets;
using OpenTicket.Domain.Sprints;
using OpenTicket.Domain.Tickets;
using OpenTicket.Domain.Users;
using OpenTicket.Infrastructure.Persistence;
using OpenTicket.IntegrationTests.Infrastructure;
using OpenTicket.IntegrationTests.Tickets;

namespace OpenTicket.IntegrationTests.Sprints;

/// <summary>
/// Sprint planning through the API. Tests in this collection share one database and run one at a time, so every
/// test that starts a sprint completes it again: other tests expect a board without an active sprint.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class SprintEndpointTests(OpenTicketApiFactory factory)
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private static CancellationToken Ct => TestApi.Ct;

    [Fact]
    public async Task Create_AsDeveloper_IsPlanned_AndReporterIsForbidden()
    {
        using var developer = await factory.CreateClientForNewUserAsync(UserRole.Agent);
        using var reporter = await factory.CreateClientForNewUserAsync(UserRole.Customer);

        var sprint = await developer.CreateSprintAsync("Sprint A", "Ship the thing");
        using var forbidden = await reporter.PostAsJsonAsync("/api/v1/sprints", new { name = "Nope" }, Ct);

        Assert.Equal(SprintState.Planned, sprint.State);
        Assert.Equal("Ship the thing", sprint.Goal);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task MoveToSprint_PlansTicket_KeepsItsStatus_AndRecordsHistory()
    {
        var category = await factory.CreateCategoryAsync();
        using var developer = await factory.CreateClientForNewUserAsync(UserRole.Agent);
        using var reporter = await factory.CreateClientForNewUserAsync(UserRole.Customer);
        var ticket = await reporter.CreateTicketAsync(category.Id);
        var sprint = await developer.CreateSprintAsync("Sprint B");

        var planned = await developer.MoveToSprintOkAsync(ticket, sprint.Id);

        Assert.Equal(sprint.Id, planned.SprintId);
        Assert.Equal("Sprint B", planned.SprintName);
        Assert.Equal(TicketStatus.Backlog, planned.Status);
        var history = await developer.HistoryAsync(ticket.Id);
        Assert.Contains(history, h => h.ChangeType == TicketChangeType.SprintChanged && h.NewValue == "Sprint B");
        Assert.Equal("Sprint B", (await reporter.GetTicketAsync(ticket.Id)).SprintName);

        var unplanned = await developer.MoveToSprintOkAsync(planned, null);
        Assert.Null(unplanned.SprintId);
    }

    [Fact]
    public async Task Lifecycle_StartShowsSprintOnBoard_CompleteMovesUnfinishedTickets()
    {
        await CompleteAnyActiveSprintAsync();
        var category = await factory.CreateCategoryAsync();
        var developerUser = await factory.CreateUserAsync(UserRole.Agent);
        using var developer = await factory.CreateClientForAsync(developerUser);
        using var reporter = await factory.CreateClientForNewUserAsync(UserRole.Customer);
        var sprint = await developer.CreateSprintAsync("Sprint C");
        var next = await developer.CreateSprintAsync("Sprint D");
        var unfinished = await developer.MoveToSprintOkAsync(await reporter.CreateTicketAsync(category.Id, "Unfinished"), sprint.Id);
        var done = await developer.MoveToSprintOkAsync(await reporter.CreateTicketAsync(category.Id, "Finished"), sprint.Id);
        done = await developer.ChangeStatusOkAsync(await developer.AssignOkAsync(done, developerUser.Id), TicketStatus.Done);
        var outside = await reporter.CreateTicketAsync(category.Id, "Not in the sprint");

        try
        {
            using (var noDates = await developer.PostAsJsonAsync($"/api/v1/sprints/{sprint.Id}/start", new { }, Ct))
            {
                Assert.Equal(HttpStatusCode.BadRequest, noDates.StatusCode);
            }

            var started = await developer.StartSprintAsync(sprint.Id);
            Assert.Equal(SprintState.Active, started.State);
            using (var second = await developer.PostAsJsonAsync($"/api/v1/sprints/{next.Id}/start",
                new { startDate = Today, endDate = Today.AddDays(14) }, Ct))
            {
                Assert.Equal("sprint.already_active", await second.ProblemCodeAsync());
            }

            var board = await developer.BoardAsync($"?categoryId={category.Id}");
            Assert.Equal(sprint.Id, board.Sprint?.Id);
            Assert.Equal(2, board.Sprint?.TicketCount);
            Assert.Equal(1, board.Sprint?.DoneCount);
            var onBoard = board.Columns.SelectMany(c => c.Items).Select(t => t.Id).ToList();
            Assert.Contains(unfinished.Id, onBoard);
            Assert.DoesNotContain(outside.Id, onBoard);

            using (var badTarget = await developer.PostAsJsonAsync($"/api/v1/sprints/{sprint.Id}/complete",
                new { moveOpenTicketsTo = sprint.Id }, Ct))
            {
                Assert.Equal("sprint.invalid_target", await badTarget.ProblemCodeAsync());
            }

            using var complete = await developer.PostAsJsonAsync($"/api/v1/sprints/{sprint.Id}/complete",
                new { moveOpenTicketsTo = next.Id }, Ct);
            Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
            Assert.Equal(SprintState.Completed, (await complete.ReadJsonAsync<SprintSummary>()).State);
        }
        finally
        {
            await CompleteAnyActiveSprintAsync();
        }

        Assert.Equal(next.Id, (await developer.GetTicketAsync(unfinished.Id)).SprintId);
        Assert.Equal(sprint.Id, (await developer.GetTicketAsync(done.Id)).SprintId);
        Assert.Null((await developer.BoardAsync($"?categoryId={category.Id}")).Sprint);
    }

    [Fact]
    public async Task Delete_PlannedSprint_ReturnsItsTicketsToTheBacklog()
    {
        var category = await factory.CreateCategoryAsync();
        using var developer = await factory.CreateClientForNewUserAsync(UserRole.Agent);
        using var reporter = await factory.CreateClientForNewUserAsync(UserRole.Customer);
        var sprint = await developer.CreateSprintAsync("Sprint E");
        var ticket = await developer.MoveToSprintOkAsync(await reporter.CreateTicketAsync(category.Id), sprint.Id);

        using var response = await developer.DeleteAsync(new Uri($"/api/v1/sprints/{sprint.Id}", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Null((await developer.GetTicketAsync(ticket.Id)).SprintId);
    }

    [Fact]
    public async Task Delete_PlannedSprintHoldingAClosedTicket_ReleasesIt_WithHistory()
    {
        // Regression: a ticket closed after it was planned made DeleteAsync throw (500).
        var category = await factory.CreateCategoryAsync();
        using var admin = await factory.CreateClientForNewUserAsync(UserRole.Admin);
        using var reporter = await factory.CreateClientForNewUserAsync(UserRole.Customer);
        var sprint = await admin.CreateSprintAsync("Sprint G");
        var ticket = await admin.MoveToSprintOkAsync(await reporter.CreateTicketAsync(category.Id), sprint.Id);
        ticket = await admin.ChangeStatusOkAsync(ticket, TicketStatus.Done);

        using var response = await admin.DeleteAsync(new Uri($"/api/v1/sprints/{sprint.Id}", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Null((await admin.GetTicketAsync(ticket.Id)).SprintId);
        var last = (await admin.HistoryAsync(ticket.Id))[^1];
        Assert.Equal(TicketChangeType.SprintChanged, last.ChangeType);
        Assert.Null(last.NewValue);
    }

    [Fact]
    public async Task Plan_ListsOpenSprintsWithTickets_AndTheUnplannedBacklog()
    {
        var category = await factory.CreateCategoryAsync();
        using var developer = await factory.CreateClientForNewUserAsync(UserRole.Agent);
        using var reporter = await factory.CreateClientForNewUserAsync(UserRole.Customer);
        var sprint = await developer.CreateSprintAsync("Sprint F");
        var planned = await developer.MoveToSprintOkAsync(await reporter.CreateTicketAsync(category.Id, "Planned"), sprint.Id);
        var unplanned = await reporter.CreateTicketAsync(category.Id, "Unplanned", TicketPriority.Urgent);

        using var response = await developer.GetAsync(new Uri("/api/v1/sprints/plan", UriKind.Relative), Ct);
        var plan = await response.ReadJsonAsync<SprintPlan>();
        using var forbidden = await reporter.GetAsync(new Uri("/api/v1/sprints/plan", UriKind.Relative), Ct);

        var section = Assert.Single(plan.Sprints, s => s.Sprint.Id == sprint.Id);
        Assert.Equal(planned.Id, Assert.Single(section.Tickets).Id);
        Assert.Contains(plan.Backlog.Tickets, t => t.Id == unplanned.Id);
        Assert.DoesNotContain(plan.Backlog.Tickets, t => t.Id == planned.Id);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(1, (await developer.ListAsync($"?sprintId={sprint.Id}")).TotalCount);
        Assert.Contains((await developer.ListAsync($"?unplanned=true&categoryId={category.Id}")).Items, t => t.Id == unplanned.Id);
    }

    /// <summary>Keeps the shared database free of active sprints, whatever a failed test left behind.</summary>
    private async Task CompleteAnyActiveSprintAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OpenTicketDbContext>();
        foreach (var active in await db.Sprints.Where(s => s.State == SprintState.Active).ToListAsync(Ct))
        {
            active.Complete(DateTime.UtcNow);
        }

        await db.SaveChangesAsync(Ct);
    }
}

internal static class SprintApi
{
    public static async Task<SprintSummary> CreateSprintAsync(this HttpClient client, string name, string? goal = null)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/sprints", new { name, goal }, TestApi.Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.ReadJsonAsync<SprintSummary>();
    }

    public static async Task<SprintSummary> StartSprintAsync(this HttpClient client, Guid sprintId)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        using var response = await client.PostAsJsonAsync($"/api/v1/sprints/{sprintId}/start",
            new { startDate = today, endDate = today.AddDays(14) }, TestApi.Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadJsonAsync<SprintSummary>();
    }

    public static async Task<TicketDetail> MoveToSprintOkAsync(this HttpClient client, TicketDetail ticket, Guid? sprintId)
    {
        using var response = await client.PutAsJsonAsync($"/api/v1/tickets/{ticket.Id}/sprint",
            new { sprintId, version = ticket.Version }, TestApi.Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadJsonAsync<TicketDetail>();
    }
}

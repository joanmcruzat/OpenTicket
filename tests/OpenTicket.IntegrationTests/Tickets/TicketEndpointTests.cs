using System.Net;
using System.Net.Http.Json;

using OpenTicket.Application.Common;
using OpenTicket.Application.Tickets;
using OpenTicket.Domain.Tickets;
using OpenTicket.Domain.Users;
using OpenTicket.IntegrationTests.Infrastructure;

namespace OpenTicket.IntegrationTests.Tickets;

[Collection(ApiTestGroup.Name)]
public sealed class TicketEndpointTests(OpenTicketApiFactory factory)
{
    private static CancellationToken Ct => TestApi.Ct;

    [Fact]
    public async Task Create_AsReporter_Returns201BacklogTicketWithTypeNumberAndNames()
    {
        var category = await factory.CreateCategoryAsync();
        using var customer = await factory.CreateClientForNewUserAsync(UserRole.Customer);

        var ticket = await customer.CreateTicketAsync(category.Id, "  Wi-Fi down  ", TicketPriority.High, TicketType.Story);

        Assert.Equal(TicketStatus.Backlog, ticket.Status);
        Assert.Equal(TicketType.Story, ticket.Type);
        Assert.Equal("Wi-Fi down", ticket.Title);
        Assert.True(ticket.Number > 0);
        Assert.Equal(category.Name, ticket.CategoryName);
        Assert.Equal("Test Customer", ticket.CreatedByName);
        Assert.Null(ticket.AssignedToId);
        Assert.True(ticket.CanEdit);
        Assert.Equal([TicketStatus.WontDo], ticket.AllowedStatuses);
    }
    [Fact]
    public async Task Create_AsDeveloperWithoutType_Returns201Task()
    {
        var category = await factory.CreateCategoryAsync();
        using var agent = await factory.CreateClientForNewUserAsync(UserRole.Agent);

        using var response = await agent.PostAsJsonAsync("/api/v1/tickets",
            new { title = "t", description = "d", categoryId = category.Id, priority = "Low" }, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(TicketType.Task, (await response.ReadJsonAsync<TicketDetail>()).Type);
    }
    [Fact]
    public async Task Create_InactiveCategory_Returns400OnCategoryId()
    {
        var category = await factory.CreateCategoryAsync(isActive: false);
        using var customer = await factory.CreateClientForNewUserAsync(UserRole.Customer);

        using var response = await customer.PostAsJsonAsync("/api/v1/tickets",
            new { title = "t", description = "d", categoryId = category.Id, priority = "Low" }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await response.ReadProblemAsync()).GetProperty("errors").TryGetProperty("categoryId", out _));
    }

    [Fact]
    public async Task Create_MissingFieldsOrUnknownEnum_Returns400()
    {
        using var customer = await factory.CreateClientForNewUserAsync(UserRole.Customer);

        using var missing = await customer.PostAsJsonAsync("/api/v1/tickets", new { title = "only a title" }, Ct);
        using var badEnum = await customer.PostAsJsonAsync("/api/v1/tickets",
            new { title = "t", description = "d", categoryId = Guid.NewGuid(), priority = "Critical" }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, badEnum.StatusCode);
    }

    [Fact]
    public async Task Get_ReportersSeeOwnTickets_StaffSeeAll()
    {
        var category = await factory.CreateCategoryAsync();
        using var owner = await factory.CreateClientForNewUserAsync(UserRole.Customer);
        using var otherCustomer = await factory.CreateClientForNewUserAsync(UserRole.Customer);
        using var developer = await factory.CreateClientForNewUserAsync(UserRole.Agent);
        using var admin = await factory.CreateClientForNewUserAsync(UserRole.Admin);
        var ticket = await owner.CreateTicketAsync(category.Id);

        using var hidden = await otherCustomer.GetAsync(new Uri($"/api/v1/tickets/{ticket.Id}", UriKind.Relative), Ct);
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        Assert.Equal("ticket.not_found", await hidden.ProblemCodeAsync());

        Assert.Equal(ticket.Id, (await admin.GetTicketAsync(ticket.Id)).Id);
        var seenByDeveloper = await developer.GetTicketAsync(ticket.Id);
        Assert.False(seenByDeveloper.CanEdit);
        Assert.Empty(seenByDeveloper.AllowedStatuses);
    }
    [Fact]
    public async Task List_ReporterSeesOwnScope_DeveloperSeesEverything()
    {
        var category = await factory.CreateCategoryAsync();
        using var agent = await factory.CreateClientForNewUserAsync(UserRole.Agent);
        using var customer = await factory.CreateClientForNewUserAsync(UserRole.Customer);
        using var otherCustomer = await factory.CreateClientForNewUserAsync(UserRole.Customer);
        var mine = await customer.CreateTicketAsync(category.Id);
        await customer.CreateTicketAsync(category.Id);
        var theirs = await otherCustomer.CreateTicketAsync(category.Id);

        var customerPage = await customer.ListAsync("");
        var agentPage = await agent.ListAsync($"?categoryId={category.Id}");

        Assert.Equal(2, customerPage.TotalCount);
        Assert.Contains(customerPage.Items, t => t.Id == mine.Id);
        Assert.DoesNotContain(customerPage.Items, t => t.Id == theirs.Id);
        Assert.Equal(3, agentPage.TotalCount);
    }
    [Fact]
    public async Task List_FiltersSearchesSortsAndPages()
    {
        var category = await factory.CreateCategoryAsync();
        using var customer = await factory.CreateClientForNewUserAsync(UserRole.Customer);
        var low = await customer.CreateTicketAsync(category.Id, "Keyboard sticky", TicketPriority.Low, TicketType.Bug);
        var urgent = await customer.CreateTicketAsync(category.Id, "Server room flooding", TicketPriority.Urgent, TicketType.Story);
        await customer.CreateTicketAsync(category.Id, "Mouse broken", TicketPriority.Medium, TicketType.Story);

        var byPriority = await customer.ListAsync("?priority=Urgent&priority=Low&sortBy=priority&sortDir=desc");
        Assert.Equal([urgent.Id, low.Id], byPriority.Items.Select(t => t.Id));

        Assert.Equal(urgent.Id, Assert.Single((await customer.ListAsync("?search=FLOOD")).Items).Id);
        Assert.Equal(low.Id, Assert.Single((await customer.ListAsync($"?search=OT-{low.Number}")).Items).Id);
        Assert.Equal(3, (await customer.ListAsync("?status=Backlog")).TotalCount);
        Assert.Equal(2, (await customer.ListAsync("?type=Story")).TotalCount);

        var paged = await customer.ListAsync("?page=2&pageSize=2&sortBy=number&sortDir=asc");
        Assert.Equal(3, paged.TotalCount);
        Assert.Equal(2, paged.TotalPages);
        Assert.Single(paged.Items);
    }

    [Theory]
    [InlineData("?sortBy=password")]
    [InlineData("?pageSize=101")]
    [InlineData("?page=0")]
    [InlineData("?sortDir=sideways")]
    [InlineData("?status=Exploded")]
    [InlineData("?type=Epic")]
    public async Task List_InvalidQuery_Returns400(string query)
    {
        using var customer = await factory.CreateClientForNewUserAsync(UserRole.Customer);

        using var response = await customer.GetAsync(new Uri($"/api/v1/tickets{query}", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_WithStaleVersion_Returns409()
    {
        var category = await factory.CreateCategoryAsync();
        using var customer = await factory.CreateClientForNewUserAsync(UserRole.Customer);
        var ticket = await customer.CreateTicketAsync(category.Id);
        using var first = await customer.PatchAsJsonAsync($"/api/v1/tickets/{ticket.Id}", new { title = "First edit", version = ticket.Version }, Ct);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        using var stale = await customer.PatchAsJsonAsync($"/api/v1/tickets/{ticket.Id}", new { title = "Second edit", version = ticket.Version }, Ct);

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("ticket.stale_version", await stale.ProblemCodeAsync());
    }

    [Fact]
    public async Task Update_ReporterAfterWorkStarted_Returns422()
    {
        var category = await factory.CreateCategoryAsync();
        var agentUser = await factory.CreateUserAsync(UserRole.Agent);
        using var agent = await factory.CreateClientForAsync(agentUser);
        using var customer = await factory.CreateClientForNewUserAsync(UserRole.Customer);
        using var admin = await factory.CreateClientForNewUserAsync(UserRole.Admin);
        var ticket = await admin.AssignOkAsync(await customer.CreateTicketAsync(category.Id), agentUser.Id);
        ticket = await agent.ChangeStatusOkAsync(ticket, TicketStatus.InProgress);

        using var response = await customer.PatchAsJsonAsync($"/api/v1/tickets/{ticket.Id}", new { title = "Too late", version = ticket.Version }, Ct);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("ticket.not_editable", await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task Update_AssignedDeveloperChangesType_RecordsHistory()
    {
        var category = await factory.CreateCategoryAsync();
        var agentUser = await factory.CreateUserAsync(UserRole.Agent);
        using var agent = await factory.CreateClientForAsync(agentUser);
        using var customer = await factory.CreateClientForNewUserAsync(UserRole.Customer);
        var ticket = await agent.AssignOkAsync(await customer.CreateTicketAsync(category.Id), agentUser.Id);

        using var response = await agent.PatchAsJsonAsync($"/api/v1/tickets/{ticket.Id}",
            new { type = "Improvement", title = "Better login button", version = ticket.Version }, TestApi.Json, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(TicketType.Improvement, (await response.ReadJsonAsync<TicketDetail>()).Type);
        var history = await agent.HistoryAsync(ticket.Id);
        Assert.Contains(history, h => h.ChangeType == TicketChangeType.TypeChanged && h.OldValue == "Bug" && h.NewValue == "Improvement");
    }
    [Fact]
    public async Task FullWorkflow_DeveloperTakesTicketThroughReviewToDone_ThenItIsReadOnly()
    {
        var category = await factory.CreateCategoryAsync();
        var agentUser = await factory.CreateUserAsync(UserRole.Agent);
        using var agent = await factory.CreateClientForAsync(agentUser);
        using var customer = await factory.CreateClientForNewUserAsync(UserRole.Customer);

        var ticket = await customer.CreateTicketAsync(category.Id);
        using (var notAssigned = await agent.ChangeStatusAsync(ticket, TicketStatus.InProgress))
        {
            Assert.Equal(HttpStatusCode.Forbidden, notAssigned.StatusCode);
        }

        ticket = await agent.AssignOkAsync(ticket, agentUser.Id);
        Assert.Equal("Test Agent", ticket.AssignedToName);
        foreach (var status in new[] { TicketStatus.ToDo, TicketStatus.InProgress, TicketStatus.InReview, TicketStatus.Done })
        {
            ticket = await agent.ChangeStatusOkAsync(ticket, status);
        }

        Assert.Equal(TicketStatus.Done, ticket.Status);
        Assert.NotNull(ticket.ClosedAtUtc);
        Assert.Equal([TicketStatus.ToDo], ticket.AllowedStatuses);

        using var comment = await customer.PostAsJsonAsync($"/api/v1/tickets/{ticket.Id}/comments", new { body = "Thanks!" }, Ct);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, comment.StatusCode);
        Assert.Equal("ticket.closed", await comment.ProblemCodeAsync());

        var history = await customer.HistoryAsync(ticket.Id);
        Assert.Equal(
            [TicketChangeType.Created, TicketChangeType.Assigned, .. Enumerable.Repeat(TicketChangeType.StatusChanged, 4)],
            history.Select(h => h.ChangeType));
        Assert.Equal("Test Agent", history[1].NewValue);
        Assert.Equal("Test Agent", history[1].ChangedByName);
    }

    [Fact]
    public async Task ChangeStatus_AdminMovesAnyTicket_ButClosedTicketsOnlyReopenToToDo()
    {
        var category = await factory.CreateCategoryAsync();
        using var customer = await factory.CreateClientForNewUserAsync(UserRole.Customer);
        using var admin = await factory.CreateClientForNewUserAsync(UserRole.Admin);
        var ticket = await customer.CreateTicketAsync(category.Id);

        ticket = await admin.ChangeStatusOkAsync(ticket, TicketStatus.Done);

        using (var skip = await admin.ChangeStatusAsync(ticket, TicketStatus.InProgress))
        {
            Assert.Equal(HttpStatusCode.UnprocessableEntity, skip.StatusCode);
            Assert.Equal("ticket.invalid_transition", await skip.ProblemCodeAsync());
        }

        using (var unassigned = await admin.ChangeStatusAsync(await admin.ChangeStatusOkAsync(ticket, TicketStatus.ToDo), TicketStatus.InReview))
        {
            Assert.Equal("ticket.not_assigned", await unassigned.ProblemCodeAsync());
        }
    }
    [Fact]
    public async Task Reopen_ByReporter_ReturnsDoneTicketToToDoAndKeepsAssignee()
    {
        var category = await factory.CreateCategoryAsync();
        var agentUser = await factory.CreateUserAsync(UserRole.Agent);
        using var agent = await factory.CreateClientForAsync(agentUser);
        using var customer = await factory.CreateClientForNewUserAsync(UserRole.Customer);
        var ticket = await agent.AssignOkAsync(await customer.CreateTicketAsync(category.Id), agentUser.Id);
        ticket = await agent.ChangeStatusOkAsync(ticket, TicketStatus.Done);

        ticket = await customer.ChangeStatusOkAsync(ticket, TicketStatus.ToDo);

        Assert.Equal(TicketStatus.ToDo, ticket.Status);
        Assert.Null(ticket.ClosedAtUtc);
        Assert.Equal(agentUser.Id, ticket.AssignedToId);
    }
    [Fact]
    public async Task WontDo_ByReporterWithReason_Closes_AndBlankReasonIs400()
    {
        var category = await factory.CreateCategoryAsync();
        using var customer = await factory.CreateClientForNewUserAsync(UserRole.Customer);
        var ticket = await customer.CreateTicketAsync(category.Id);

        using (var blank = await customer.ChangeStatusAsync(ticket, TicketStatus.WontDo, "  "))
        {
            Assert.Equal(HttpStatusCode.BadRequest, blank.StatusCode);
            Assert.Equal("ticket.reason_required", await blank.ProblemCodeAsync());
        }

        var dropped = await customer.ChangeStatusOkAsync(ticket, TicketStatus.WontDo, "Fixed it myself");

        Assert.Equal(TicketStatus.WontDo, dropped.Status);
        Assert.Equal("Fixed it myself", dropped.ClosureNote);
        Assert.NotNull(dropped.ClosedAtUtc);
    }
    [Fact]
    public async Task Assign_ToNonDeveloper_Returns400_ReporterIs403()
    {
        var category = await factory.CreateCategoryAsync();
        var customerUser = await factory.CreateUserAsync(UserRole.Customer);
        using var customer = await factory.CreateClientForAsync(customerUser);
        using var admin = await factory.CreateClientForNewUserAsync(UserRole.Admin);
        var ticket = await customer.CreateTicketAsync(category.Id);

        using var toCustomer = await admin.AssignAsync(ticket, customerUser.Id);
        using var byCustomer = await customer.AssignAsync(ticket, customerUser.Id);

        Assert.Equal(HttpStatusCode.BadRequest, toCustomer.StatusCode);
        Assert.Equal("ticket.assignee_invalid", await toCustomer.ProblemCodeAsync());
        Assert.Equal(HttpStatusCode.Forbidden, byCustomer.StatusCode);
    }

    [Fact]
    public async Task SelfAssign_DeveloperTakesUnassignedTicket_ButNotSomeoneElses()
    {
        var category = await factory.CreateCategoryAsync();
        var first = await factory.CreateUserAsync(UserRole.Agent);
        var second = await factory.CreateUserAsync(UserRole.Agent);
        using var firstClient = await factory.CreateClientForAsync(first);
        using var secondClient = await factory.CreateClientForAsync(second);
        using var customer = await factory.CreateClientForNewUserAsync(UserRole.Customer);
        var ticket = await customer.CreateTicketAsync(category.Id);

        ticket = await firstClient.AssignOkAsync(ticket, first.Id);
        using var steal = await secondClient.AssignAsync(ticket, second.Id);
        using var assignOther = await firstClient.AssignAsync(ticket, second.Id);

        Assert.Equal(first.Id, ticket.AssignedToId);
        Assert.Equal("ticket.already_assigned", await steal.ProblemCodeAsync());
        Assert.Equal(HttpStatusCode.Forbidden, assignOther.StatusCode);
    }
    [Fact]
    public async Task Unassign_BeforeWorkStarts_ClearsAssignee_ButNotInProgress()
    {
        var category = await factory.CreateCategoryAsync();
        var agentUser = await factory.CreateUserAsync(UserRole.Agent);
        using var agent = await factory.CreateClientForAsync(agentUser);
        using var customer = await factory.CreateClientForNewUserAsync(UserRole.Customer);
        using var admin = await factory.CreateClientForNewUserAsync(UserRole.Admin);
        var ticket = await admin.AssignOkAsync(await customer.CreateTicketAsync(category.Id), agentUser.Id);
        var working = await agent.ChangeStatusOkAsync(
            await admin.AssignOkAsync(await customer.CreateTicketAsync(category.Id), agentUser.Id), TicketStatus.InProgress);

        using var response = await agent.DeleteAsync(new Uri($"/api/v1/tickets/{ticket.Id}/assignee?version={ticket.Version}", UriKind.Relative), Ct);
        using var blocked = await admin.DeleteAsync(new Uri($"/api/v1/tickets/{working.Id}/assignee?version={working.Version}", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null((await response.ReadJsonAsync<TicketDetail>()).AssignedToId);
        Assert.Equal("ticket.unassign_in_progress", await blocked.ProblemCodeAsync());
    }

    [Fact]
    public async Task Board_GroupsVisibleTicketsIntoColumns_WithFiltersAndAllowedMoves()
    {
        var category = await factory.CreateCategoryAsync();
        var agentUser = await factory.CreateUserAsync(UserRole.Agent);
        using var agent = await factory.CreateClientForAsync(agentUser);
        using var customer = await factory.CreateClientForNewUserAsync(UserRole.Customer);
        var low = await customer.CreateTicketAsync(category.Id, "Low bug", TicketPriority.Low);
        var urgent = await customer.CreateTicketAsync(category.Id, "Urgent bug", TicketPriority.Urgent);
        var story = await customer.CreateTicketAsync(category.Id, "A story", TicketPriority.Medium, TicketType.Story);
        await agent.ChangeStatusOkAsync(await agent.AssignOkAsync(story, agentUser.Id), TicketStatus.InProgress);
        await customer.ChangeStatusOkAsync(await customer.CreateTicketAsync(category.Id, "Dropped"), TicketStatus.WontDo, "dup");

        var board = (await agent.BoardAsync($"?categoryId={category.Id}")).Columns;
        var bugsOnly = (await agent.BoardAsync($"?categoryId={category.Id}&type=Bug")).Columns;

        Assert.Equal(
            [TicketStatus.Backlog, TicketStatus.ToDo, TicketStatus.InProgress, TicketStatus.InReview, TicketStatus.Done],
            board.Select(c => c.Status));
        var backlog = board[0];
        Assert.Equal(2, backlog.Total);
        Assert.Equal([urgent.Id, low.Id], backlog.Items.Select(t => t.Id));
        var inProgress = Assert.Single(board[2].Items);
        Assert.Equal(story.Id, inProgress.Id);
        Assert.Contains(TicketStatus.InReview, inProgress.AllowedStatuses);
        Assert.Empty(backlog.Items[0].AllowedStatuses);
        Assert.Equal(0, bugsOnly[2].Total);
    }
}

internal static class TicketListExtensions
{
    public static async Task<TicketBoard> BoardAsync(this HttpClient client, string query)
    {
        using var response = await client.GetAsync(new Uri($"/api/v1/tickets/board{query}", UriKind.Relative), TestApi.Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadJsonAsync<TicketBoard>();
    }

    public static async Task<List<TicketHistoryEntry>> HistoryAsync(this HttpClient client, Guid ticketId)
    {
        using var response = await client.GetAsync(new Uri($"/api/v1/tickets/{ticketId}/history", UriKind.Relative), TestApi.Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadJsonAsync<List<TicketHistoryEntry>>();
    }

    public static async Task<PagedResult<TicketSummary>> ListAsync(this HttpClient client, string query)
    {
        using var response = await client.GetAsync(new Uri($"/api/v1/tickets{query}", UriKind.Relative), TestApi.Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadJsonAsync<PagedResult<TicketSummary>>();
    }
}

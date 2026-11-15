using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using OpenTicket.API.Endpoints;
using OpenTicket.Domain.Tickets;
using OpenTicket.Domain.Users;
using OpenTicket.Infrastructure.Demo;
using OpenTicket.Infrastructure.Persistence;
using OpenTicket.IntegrationTests.Infrastructure;
using OpenTicket.IntegrationTests.Sprints;
using OpenTicket.IntegrationTests.Tickets;

namespace OpenTicket.IntegrationTests.Auth;

[Collection(ApiTestGroup.Name)]
public sealed class DemoModeOffTests(OpenTicketApiFactory factory)
{
    [Fact]
    public async Task DemoSignIn_WhenDemoModeIsOff_Returns404()
    {
        using var client = factory.CreateApiClient();

        using var response = await client.PostAsync(new Uri("/api/v1/auth/demo/admin", UriKind.Relative), null, TestApi.Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

[Collection(DemoTestGroup.Name)]
public sealed class DemoModeTests(DemoApiFactory factory)
{
    private const int ScenarioTicketCount = 17;

    private static CancellationToken Ct => TestApi.Ct;

    [Fact]
    public async Task Options_InDemoMode_AnnounceDemoAndHidePasswordReset()
    {
        using var client = CreateClient(factory);

        using var response = await client.GetAsync(new Uri("/api/v1/auth/options", UriKind.Relative), Ct);
        var options = await response.ReadJsonAsync<OptionsDto>();

        Assert.Equal(6, options.Demo?.ResetIntervalHours);
        Assert.False(options.PasswordReset);
    }

    [Theory]
    [InlineData("admin", UserRole.Admin, "Morgan Lee")]
    [InlineData("developer", UserRole.Agent, "Alex Rivera")]
    [InlineData("reporter", UserRole.Customer, "Jordan Kim")]
    public async Task DemoSignIn_EachRole_StartsSessionWithoutPassword(string role, UserRole expectedRole, string expectedName)
    {
        using var client = CreateClient(factory);

        using var response = await client.PostAsync(new Uri($"/api/v1/auth/demo/{role}", UriKind.Relative), null, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = await response.ReadJsonAsync<SessionResponse>();
        Assert.Equal(expectedRole, session.User.Role);
        Assert.Equal(expectedName, session.User.DisplayName);
        Assert.NotNull(response.GetRefreshCookie());
    }

    [Fact]
    public async Task DemoData_CoversEveryStatusAndType_AndDeveloperSeesItOnTheBoard()
    {
        using var client = await SignInAsync(factory, "developer");

        var tickets = await client.ListAsync("?pageSize=100&search=&sortBy=number&sortDir=asc");
        var demoTickets = tickets.Items.Where(t => t.CreatedByName is "Jordan Kim" or "Taylor Brooks" or "Morgan Lee").ToList();

        Assert.Equal(ScenarioTicketCount, demoTickets.Count);
        Assert.Equal(Enum.GetValues<TicketStatus>().Order(), demoTickets.Select(t => t.Status).Distinct().Order());
        Assert.Equal(Enum.GetValues<TicketType>().Order(), demoTickets.Select(t => t.Type).Distinct().Order());
        Assert.Contains(demoTickets, t => t.AssignedToName == "Alex Rivera" && t.Status == TicketStatus.InProgress);
    }

    [Fact]
    public async Task DemoData_HasAnActiveSprintThatTheBoardShows()
    {
        using var client = await SignInAsync(factory, "developer");

        var board = await client.BoardAsync("");

        Assert.Equal("Sprint 4", board.Sprint?.Name);
        Assert.Equal(7, board.Columns.Sum(c => c.Total));
        Assert.All(board.Columns.SelectMany(c => c.Items), t => Assert.Equal("Sprint 4", t.SprintName));
    }

    [Fact]
    public async Task Register_InDemoMode_IsTurnedOff()
    {
        using var client = CreateClient(factory);

        using var response = await client.PostAsJsonAsync("/api/v1/auth/register",
            new { email = $"visitor-{Guid.NewGuid():N}@example.test", password = "long-enough-password", displayName = "Visitor" }, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("auth.registration_disabled", await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task DemoAdmin_CannotChangeDemoAccounts()
    {
        using var admin = await SignInAsync(factory, "admin");
        var alexId = await UserIdAsync(factory, "alex");

        using var deactivate = await admin.PostAsync(new Uri($"/api/v1/users/{alexId}/deactivate", UriKind.Relative), null, Ct);
        using var demote = await admin.PutAsJsonAsync($"/api/v1/users/{alexId}/role", new { role = "Customer" }, TestApi.Json, Ct);

        Assert.Equal("user.demo_protected", await deactivate.ProblemCodeAsync());
        Assert.Equal("user.demo_protected", await demote.ProblemCodeAsync());
    }

    [Fact]
    public async Task Reset_RestoresDemoTickets_AndLeavesOtherUsersTicketsAlone()
    {
        using var reporter = await SignInAsync(factory, "reporter");
        var category = await factory.CreateCategoryAsync();
        await reporter.CreateTicketAsync(category.Id, "Visitor's extra ticket");
        using var realUser = await factory.CreateClientForNewUserAsync(UserRole.Customer);
        var keep = await realUser.CreateTicketAsync(category.Id, "Real ticket that must survive");
        using var admin = await SignInAsync(factory, "admin");
        var demoSprint = (await admin.BoardAsync("")).Sprint!;
        var planned = await admin.MoveToSprintOkAsync(await realUser.CreateTicketAsync(category.Id, "Real ticket in a demo sprint"), demoSprint.Id);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<DemoDataSeeder>().ResetAsync(Ct);
        }

        await using var check = factory.Services.CreateAsyncScope();
        var db = check.ServiceProvider.GetRequiredService<OpenTicketDbContext>();
        var demoUsers = db.Users.Where(u => u.Email!.EndsWith("@" + DemoAccounts.EmailDomain)).Select(u => u.Id);
        Assert.Equal(ScenarioTicketCount, await db.Tickets.CountAsync(t => demoUsers.Contains(t.CreatedById), Ct));
        Assert.False(await db.Tickets.AnyAsync(t => t.Title == "Visitor's extra ticket", Ct));
        Assert.True(await db.Tickets.AnyAsync(t => t.Id == keep.Id, Ct));
        // The real ticket left the deleted demo sprint through the domain, so its history says so.
        var released = await db.Tickets.SingleAsync(t => t.Id == planned.Id, Ct);
        Assert.Null(released.SprintId);
        Assert.True(await db.TicketHistory.AnyAsync(
            h => h.TicketId == planned.Id && h.ChangeType == TicketChangeType.SprintChanged && h.NewValue == null, Ct));
    }

    private static HttpClient CreateClient(WebApplicationFactory<Program> demo) =>
        demo.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = false,
            AllowAutoRedirect = false,
        });

    private static async Task<HttpClient> SignInAsync(WebApplicationFactory<Program> demo, string role)
    {
        var client = CreateClient(demo);
        using var response = await client.PostAsync(new Uri($"/api/v1/auth/demo/{role}", UriKind.Relative), null, Ct);
        response.EnsureSuccessStatusCode();
        var session = await response.ReadJsonAsync<SessionResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        return client;
    }

    private static async Task<Guid> UserIdAsync(WebApplicationFactory<Program> demo, string key)
    {
        await using var scope = demo.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OpenTicketDbContext>();
        return await db.Users.Where(u => u.Email == $"{key}@{DemoAccounts.EmailDomain}").Select(u => u.Id).SingleAsync(Ct);
    }

    private sealed record OptionsDto(List<string> ExternalProviders, bool PasswordReset, DemoDto? Demo);

    private sealed record DemoDto(int ResetIntervalHours);
}

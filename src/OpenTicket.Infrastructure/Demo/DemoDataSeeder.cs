using System.Security.Cryptography;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using OpenTicket.Domain.Categories;
using OpenTicket.Domain.Common;
using OpenTicket.Domain.Sprints;
using OpenTicket.Domain.Tickets;
using OpenTicket.Domain.Users;
using OpenTicket.Infrastructure.Identity;
using OpenTicket.Infrastructure.Persistence;

namespace OpenTicket.Infrastructure.Demo;

/// <summary>
/// Creates the demo cast, categories and sample tickets, and puts them back to their starting state on reset.
/// Only tickets reported by demo accounts are deleted; everything else in the database is left alone.
/// </summary>
internal sealed partial class DemoDataSeeder(
    OpenTicketDbContext db,
    UserManager<ApplicationUser> users,
    TimeProvider clock,
    ILogger<DemoDataSeeder> logger)
{
    /// <summary>Seeds on first start; later starts keep whatever visitors have done until the next reset.</summary>
    public async Task EnsureSeededAsync(CancellationToken ct)
    {
        var people = await EnsureAccountsAsync(ct);
        var demoIds = people.Values.Select(u => u.Id).ToList();
        if (await db.Tickets.AnyAsync(t => demoIds.Contains(t.CreatedById), ct))
        {
            return;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await SeedTicketsAsync(people, ct);
        await transaction.CommitAsync(ct);
        LogSeeded(DemoScenario.Tickets.Count);
    }

    public async Task ResetAsync(CancellationToken ct)
    {
        var people = await EnsureAccountsAsync(ct);
        var demoIds = people.Values.Select(u => u.Id).ToList();

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // Comments and history go with their ticket (ON DELETE CASCADE).
        var deleted = await db.Tickets.Where(t => demoIds.Contains(t.CreatedById)).ExecuteDeleteAsync(ct);

        // Demo sprints go too. Any other user's ticket planned into one returns to the backlog first, through the
        // domain so the change lands in its history (every ticket change is recorded).
        var demoSprints = db.Sprints.Where(s => demoIds.Contains(s.CreatedById)).Select(s => s.Id);
        var stranded = await db.Tickets.Where(t => t.SprintId != null && demoSprints.Contains(t.SprintId.Value)).ToListAsync(ct);
        foreach (var ticket in stranded)
        {
            ticket.DetachFromDeletedSprint(people[DemoAccounts.Admin].Id, clock.GetUtcNow().UtcDateTime);
        }

        await db.SaveChangesAsync(ct);
        await db.Sprints.Where(s => demoIds.Contains(s.CreatedById)).ExecuteDeleteAsync(ct);
        await SeedTicketsAsync(people, ct);
        await transaction.CommitAsync(ct);
        LogReset(deleted, DemoScenario.Tickets.Count);
    }

    /// <summary>Creates missing demo users and restores their role and active state (visitors may not change them).</summary>
    private async Task<Dictionary<DemoPerson, ApplicationUser>> EnsureAccountsAsync(CancellationToken ct)
    {
        var people = new Dictionary<DemoPerson, ApplicationUser>();
        foreach (var person in DemoAccounts.All)
        {
            ct.ThrowIfCancellationRequested();
            var user = await users.FindByEmailAsync(person.Email);
            if (user is null)
            {
                user = new ApplicationUser
                {
                    UserName = person.Email,
                    Email = person.Email,
                    DisplayName = person.DisplayName,
                    CreatedAtUtc = clock.GetUtcNow().UtcDateTime,
                };

                // A random password nobody knows: demo visitors sign in with the one-click demo endpoint only.
                EnsureSucceeded(await users.CreateAsync(user, Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))));
            }

            if (!user.IsActive)
            {
                user.IsActive = true;
                EnsureSucceeded(await users.UpdateAsync(user));
            }

            var roles = await users.GetRolesAsync(user);
            if (roles.Count != 1 || roles[0] != person.Role.ToString())
            {
                EnsureSucceeded(await users.RemoveFromRolesAsync(user, roles));
                EnsureSucceeded(await users.AddToRoleAsync(user, person.Role.ToString()));
            }

            people[person] = user;
        }

        return people;
    }

    private async Task SeedTicketsAsync(Dictionary<DemoPerson, ApplicationUser> people, CancellationToken ct)
    {
        var categories = await EnsureCategoriesAsync(ct);
        var now = clock.GetUtcNow().UtcDateTime;
        var admin = Actor(people, DemoAccounts.Admin);
        var sprints = await CreateSprintsAsync(admin, now, ct);

        // Oldest first, saved one by one, so ticket numbers follow creation order.
        foreach (var spec in DemoScenario.Tickets.OrderByDescending(t => t.DaysAgo))
        {
            var created = now.AddDays(-spec.DaysAgo);
            var steps = StatusPath(spec.Status);
            var stepCount = steps.Count + spec.Comments.Count + (spec.Assignee is null ? 0 : 1);
            var spacing = TimeSpan.FromMinutes(Math.Min(360, (spec.DaysAgo * 24 * 60 - 60) / Math.Max(1, stepCount + 1)));
            var at = created;
            DateTime Next() => at += spacing;

            var reporter = Actor(people, spec.Reporter);
            var ticket = Ticket.Create(
                spec.Title, spec.Description, spec.Type, categories[spec.Category], spec.Priority, reporter.UserId, created);
            db.Tickets.Add(ticket);

            var mover = admin;
            if (spec.Assignee is { } assignee)
            {
                var developer = Actor(people, assignee);
                Check(ticket.Assign(developer.UserId, admin, Next()));
                mover = developer;
            }

            if (sprints.TryGetValue(DemoScenario.SprintOf(spec), out var sprint))
            {
                Check(ticket.MoveToSprint(sprint, admin, at));
            }

            // Comments are spread through the ticket's life, between the status changes.
            var comments = new Queue<DemoComment>(spec.Comments);
            foreach (var status in steps)
            {
                if (comments.TryDequeue(out var early))
                {
                    db.TicketComments.Add(new TicketComment(ticket.Id, people[early.Author].Id, early.Body, Next()));
                }

                var reason = status == TicketStatus.WontDo ? spec.WontDoReason : null;
                Check(ticket.ChangeStatus(status, reason, status == TicketStatus.WontDo ? admin : mover, Next()));
            }

            while (comments.TryDequeue(out var late))
            {
                db.TicketComments.Add(new TicketComment(ticket.Id, people[late.Author].Id, late.Body, Next()));
            }

            await db.SaveChangesAsync(ct);
        }

        await FinishSprintsAsync(sprints, now, ct);
    }

    /// <summary>All three demo sprints start planned so tickets can join them; <see cref="FinishSprintsAsync"/> moves them on.</summary>
    private async Task<Dictionary<DemoSprint, Sprint>> CreateSprintsAsync(Actor admin, DateTime now, CancellationToken ct)
    {
        var sprints = new Dictionary<DemoSprint, Sprint>();
        foreach (var spec in DemoScenario.Sprints)
        {
            var sprint = Sprint.Create(spec.Name, spec.Goal, admin.UserId, now.AddDays(-21)).Value!;
            db.Sprints.Add(sprint);
            sprints[spec.Key] = sprint;
        }

        await db.SaveChangesAsync(ct);
        return sprints;
    }

    /// <summary>Sprint 3 ran two weeks ago and is completed; Sprint 4 is the active two-week sprint; Sprint 5 stays planned.</summary>
    private async Task FinishSprintsAsync(Dictionary<DemoSprint, Sprint> sprints, DateTime now, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(now);
        var previous = sprints[DemoSprint.Previous];
        Check(previous.Start(today.AddDays(-20), today.AddDays(-7), now.AddDays(-20)));
        Check(previous.Complete(now.AddDays(-6)));

        // Only one sprint may be active; if someone else's sprint is running, the demo's stays planned.
        if (!await db.Sprints.AnyAsync(s => s.State == SprintState.Active, ct))
        {
            Check(sprints[DemoSprint.Current].Start(today.AddDays(-6), today.AddDays(8), now.AddDays(-6)));
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task<Dictionary<string, Guid>> EnsureCategoriesAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var result = new Dictionary<string, Guid>();
        foreach (var name in DemoAccounts.Categories)
        {
            var normalized = TicketCategory.Normalize(name);
            var category = await db.TicketCategories.SingleOrDefaultAsync(c => c.NormalizedName == normalized, ct);
            if (category is null)
            {
                category = new TicketCategory(name, null, now);
                db.TicketCategories.Add(category);
            }
            else if (!category.IsActive)
            {
                category.Activate(now);
            }

            result[name] = category.Id;
        }

        await db.SaveChangesAsync(ct);
        return result;
    }

    private static IReadOnlyList<TicketStatus> StatusPath(TicketStatus target) => target switch
    {
        TicketStatus.Backlog => [],
        TicketStatus.ToDo => [TicketStatus.ToDo],
        TicketStatus.InProgress => [TicketStatus.ToDo, TicketStatus.InProgress],
        TicketStatus.InReview => [TicketStatus.ToDo, TicketStatus.InProgress, TicketStatus.InReview],
        TicketStatus.Done => [TicketStatus.ToDo, TicketStatus.InProgress, TicketStatus.InReview, TicketStatus.Done],
        _ => [TicketStatus.WontDo],
    };

    private static Actor Actor(Dictionary<DemoPerson, ApplicationUser> people, DemoPerson person) =>
        new(people[person].Id, person.Role);

    private static void Check(Result result)
    {
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException($"Demo scenario breaks a workflow rule: {result.Error!.Message}");
        }
    }

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Demo account setup failed: {result}");
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Demo data seeded with {TicketCount} tickets")]
    private partial void LogSeeded(int ticketCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "Demo data reset: {Deleted} tickets removed, {TicketCount} recreated")]
    private partial void LogReset(int deleted, int ticketCount);
}

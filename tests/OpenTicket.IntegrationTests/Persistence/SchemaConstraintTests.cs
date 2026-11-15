using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using OpenTicket.Domain.Categories;
using OpenTicket.Domain.Tickets;
using OpenTicket.Infrastructure.Identity;
using OpenTicket.Infrastructure.Persistence;
using OpenTicket.IntegrationTests.Infrastructure;

namespace OpenTicket.IntegrationTests.Persistence;

/// <summary>Verifies that the migrated schema enforces the invariants documented in docs/architecture.md §3.</summary>
[Collection(ApiTestGroup.Name)]
public sealed class SchemaConstraintTests(OpenTicketApiFactory factory)
{
    private static readonly DateTime Now = new(2026, 11, 7, 9, 0, 0, DateTimeKind.Utc);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SavingTicket_AssignsSequentialNumberAndPersistsHistory()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OpenTicketDbContext>();
        var (userId, categoryId) = await SeedUserAndCategoryAsync(db);

        // Saved one at a time, as the API does. Within a single SaveChanges EF orders inserts by key, not by Add order.
        var first = Ticket.Create("First", "d", TicketType.Task, categoryId, TicketPriority.Low, userId, Now);
        db.Tickets.Add(first);
        await db.SaveChangesAsync(Ct);
        var second = Ticket.Create("Second", "d", TicketType.Task, categoryId, TicketPriority.Low, userId, Now);
        db.Tickets.Add(second);
        await db.SaveChangesAsync(Ct);

        Assert.True(first.Number > 0);
        Assert.True(second.Number > first.Number);
        Assert.NotEqual(0u, first.Version);
        Assert.Equal(1, await db.TicketHistory.CountAsync(h => h.TicketId == first.Id, Ct));
    }

    [Fact]
    public async Task WontDoTicketWithoutNote_IsRejectedByDatabase()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OpenTicketDbContext>();
        var (userId, categoryId) = await SeedUserAndCategoryAsync(db);
        var ticket = Ticket.Create("To drop", "d", TicketType.Task, categoryId, TicketPriority.Low, userId, Now);
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync(Ct);

        var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlAsync(
            $"UPDATE ticket SET status = 5, closed_at_utc = now(), closure_note = NULL WHERE id = {ticket.Id}", Ct));

        Assert.Equal("ck_ticket_wont_do_note", error.ConstraintName);
    }

    [Fact]
    public async Task DoneTicketWithoutClosedAt_IsRejectedByDatabase()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OpenTicketDbContext>();
        var (userId, categoryId) = await SeedUserAndCategoryAsync(db);
        var ticket = Ticket.Create("To close", "d", TicketType.Task, categoryId, TicketPriority.Low, userId, Now);
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync(Ct);

        var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlAsync(
            $"UPDATE ticket SET status = 4 WHERE id = {ticket.Id}", Ct));

        Assert.Equal("ck_ticket_closed_at", error.ConstraintName);
    }

    [Fact]
    public async Task CategoryNamesDifferingOnlyByCase_AreRejectedByDatabase()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OpenTicketDbContext>();
        var name = $"Hardware-{Guid.NewGuid():N}";
        db.TicketCategories.Add(new TicketCategory(name, null, Now));
        await db.SaveChangesAsync(Ct);

        db.TicketCategories.Add(new TicketCategory(name.ToLowerInvariant(), null, Now));

        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct));
        Assert.Equal("ix_ticket_category_normalized_name", Assert.IsType<PostgresException>(error.InnerException).ConstraintName);
    }

    private static async Task<(Guid UserId, Guid CategoryId)> SeedUserAndCategoryAsync(OpenTicketDbContext db)
    {
        var unique = Guid.NewGuid().ToString("N");
        var user = new ApplicationUser
        {
            UserName = $"user-{unique}@example.test",
            NormalizedUserName = $"USER-{unique}@EXAMPLE.TEST",
            Email = $"user-{unique}@example.test",
            NormalizedEmail = $"USER-{unique}@EXAMPLE.TEST",
            DisplayName = "Test User",
            CreatedAtUtc = Now,
        };
        var category = new TicketCategory($"Category-{unique}", null, Now);
        db.Users.Add(user);
        db.TicketCategories.Add(category);
        await db.SaveChangesAsync(Ct);
        return (user.Id, category.Id);
    }
}

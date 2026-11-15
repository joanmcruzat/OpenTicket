using OpenTicket.Domain.Categories;
using OpenTicket.Domain.Common;
using OpenTicket.Domain.Tickets;
using OpenTicket.Domain.Users;

namespace OpenTicket.UnitTests.Builders;

/// <summary>A ticket plus the cast of actors around it, so each test states only what matters.</summary>
internal sealed class TicketScenario
{
    public static readonly DateTime Now = new(2026, 11, 7, 9, 0, 0, DateTimeKind.Utc);

    private TicketScenario()
    {
        Category = new TicketCategory($"Cat-{Guid.NewGuid():N}", null, Now);
        Ticket = Ticket.Create(
            "Login button unresponsive", "Clicking it does nothing", TicketType.Bug, Category.Id, TicketPriority.Medium, Owner.UserId, Now);
    }

    public Actor Admin { get; } = new(Guid.CreateVersion7(), UserRole.Admin);

    public Actor Owner { get; } = new(Guid.CreateVersion7(), UserRole.Customer);

    public Actor OtherCustomer { get; } = new(Guid.CreateVersion7(), UserRole.Customer);

    public Actor Agent { get; } = new(Guid.CreateVersion7(), UserRole.Agent);

    public Actor OtherAgent { get; } = new(Guid.CreateVersion7(), UserRole.Agent);

    public TicketCategory Category { get; }

    public Ticket Ticket { get; }

    /// <summary>A new, unassigned ticket in Backlog.</summary>
    public static TicketScenario Backlog() => new();

    public static TicketScenario Assigned() => Backlog().Do(s => s.Ticket.Assign(s.Agent.UserId, s.Admin, Now));

    public static TicketScenario InStatus(TicketStatus status) => Assigned().Do(s => s.Ticket.ChangeStatus(
        status, status == TicketStatus.WontDo ? "Duplicate of OT-1" : null, s.Admin, Now));

    public static TicketScenario InProgress() => InStatus(TicketStatus.InProgress);

    public static TicketScenario Done() => InStatus(TicketStatus.Done);

    public Actor ActorFor(string name) => name switch
    {
        nameof(Admin) => Admin,
        nameof(Owner) => Owner,
        nameof(OtherCustomer) => OtherCustomer,
        nameof(Agent) => Agent,
        nameof(OtherAgent) => OtherAgent,
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, null),
    };

    private TicketScenario Do(Func<TicketScenario, Result> step)
    {
        var result = step(this);
        Assert.True(result.IsSuccess, result.Error?.Message);
        return this;
    }
}

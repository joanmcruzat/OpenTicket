using OpenTicket.Domain.Tickets;

using static OpenTicket.Infrastructure.Demo.DemoAccounts;

namespace OpenTicket.Infrastructure.Demo;

internal sealed record DemoComment(DemoPerson Author, string Body);

/// <summary>One sample ticket and the story of how it got to its current status.</summary>
internal sealed record DemoTicket(
    string Title,
    string Description,
    TicketType Type,
    TicketPriority Priority,
    string Category,
    DemoPerson Reporter,
    DemoPerson? Assignee,
    TicketStatus Status,
    int DaysAgo,
    IReadOnlyList<DemoComment> Comments,
    string? WontDoReason = null);

internal enum DemoSprint
{
    None,
    Previous,
    Current,
    Next,
}

internal sealed record DemoSprintSpec(DemoSprint Key, string Name, string Goal);

/// <summary>A small software team's board: a mix of types, priorities, owners and every workflow status.</summary>
internal static class DemoScenario
{
    /// <summary>A completed sprint, the active one (which the board shows), and the next planned one.</summary>
    public static readonly IReadOnlyList<DemoSprintSpec> Sprints =
    [
        new(DemoSprint.Previous, "Sprint 3", "Ship the staging environment and keep dependencies current."),
        new(DemoSprint.Current, "Sprint 4", "Fix the Safari sign-in bug and make the ticket list fast."),
        new(DemoSprint.Next, "Sprint 5", "Reporting exports and reliable backups."),
    ];

    private static readonly Dictionary<string, DemoSprint> SprintByTitle = new()
    {
        ["Upgrade Npgsql to the latest patch release"] = DemoSprint.Previous,
        ["Set up a staging environment"] = DemoSprint.Previous,
        ["Login button does nothing on Safari 18"] = DemoSprint.Current,
        ["Ticket list is slow with 10,000+ tickets"] = DemoSprint.Current,
        ["Rotate the JWT signing key"] = DemoSprint.Current,
        ["Password reset email lands in spam"] = DemoSprint.Current,
        ["Comment box loses text when switching tabs"] = DemoSprint.Current,
        ["Board cards overflow on narrow phones"] = DemoSprint.Current,
        ["500 error when a category name contains an emoji"] = DemoSprint.Current,
        ["Export tickets to CSV"] = DemoSprint.Next,
        ["Nightly database backup to object storage"] = DemoSprint.Next,
    };

    public static DemoSprint SprintOf(DemoTicket ticket) => SprintByTitle.GetValueOrDefault(ticket.Title, DemoSprint.None);

    public static readonly IReadOnlyList<DemoTicket> Tickets =
    [
        new("Login button does nothing on Safari 18",
            "Steps:\n1. Open the sign-in page in Safari 18 on macOS 15\n2. Enter valid credentials\n3. Click Sign in\n\n" +
            "Expected: you are signed in.\nActual: nothing happens and no request is sent. Chrome and Firefox work.",
            TicketType.Bug, TicketPriority.High, "Frontend", Jordan, Alex, TicketStatus.InProgress, 3,
            [
                new(Jordan, "Also happens on iPadOS 18.1. Screenshot of the console shows no errors."),
                new(Alex, "Reproduced. The click handler is attached before the form finishes initialising. Working on a fix."),
            ]),
        new("Ticket list is slow with 10,000+ tickets",
            "Opening All tickets takes 3-4 seconds on our busiest project. It should feel instant.",
            TicketType.Improvement, TicketPriority.Medium, "Backend", Taylor, Priya, TicketStatus.InReview, 6,
            [
                new(Priya, "Added a composite index on (status, created_at). The list query went from 900 ms to 40 ms."),
                new(Sam, "Reviewing now. Can we check the count query as well?"),
                new(Priya, "Good catch. The count uses the same index; both are under 50 ms now."),
            ]),
        new("As a reporter I can attach screenshots to a ticket",
            "Reporters often describe UI bugs in words. Let them attach PNG or JPG images (max 5 MB) when creating " +
            "or commenting.\n\nAcceptance criteria:\n- Upload from the create form and the comment box\n- Thumbnails in the conversation",
            TicketType.Story, TicketPriority.Medium, "Frontend", Jordan, null, TicketStatus.Backlog, 9,
            [new(Admin, "Great idea. Parking it in the backlog until we pick a free storage option.")]),
        new("Rotate the JWT signing key",
            "The signing key has not been rotated since launch. Generate a new one, deploy it, and document the steps.",
            TicketType.Task, TicketPriority.Urgent, "Infrastructure", Admin, Sam, TicketStatus.ToDo, 2, []),
        new("500 error when a category name contains an emoji",
            "Admin → Categories → New category → name \"🚀 Launch\" → Save shows \"Something went wrong\".",
            TicketType.Bug, TicketPriority.Urgent, "Backend", Taylor, null, TicketStatus.ToDo, 1,
            [new(Taylor, "Same thing with accented names like \"Développement\", so it may not be emoji-specific.")]),
        new("Dark mode",
            "Offer a dark theme that follows the operating system setting, with a manual override in the account menu.",
            TicketType.Story, TicketPriority.Low, "Frontend", Jordan, null, TicketStatus.Backlog, 12, []),
        new("Upgrade Npgsql to the latest patch release",
            "A patch release fixes a connection-pool leak under load. Upgrade and run the full test suite.",
            TicketType.Task, TicketPriority.Low, "Backend", Admin, Priya, TicketStatus.Done, 10,
            [new(Priya, "Upgraded. All 170+ tests are green and the staging soak test shows a flat connection count.")]),
        new("Nightly database backup to object storage",
            "Run pg_dump every night, keep 14 days, and store the files outside the database host.",
            TicketType.Task, TicketPriority.High, "Infrastructure", Admin, null, TicketStatus.Backlog, 4, []),
        new("Board cards overflow on narrow phones",
            "On a 360 px wide screen, long ticket titles push the priority icon and avatar off the card.",
            TicketType.Bug, TicketPriority.Medium, "Mobile", Jordan, Alex, TicketStatus.Done, 8,
            [
                new(Alex, "Fixed by clamping titles to three lines and letting the meta row wrap."),
                new(Jordan, "Confirmed on my Pixel. Thanks!"),
            ]),
        new("Push notifications when a ticket is assigned to me",
            "Developers want to know right away when work lands on their plate, without keeping the board open.",
            TicketType.Story, TicketPriority.Medium, "Mobile", Taylor, null, TicketStatus.Backlog, 7, []),
        new("Show who else is viewing a ticket",
            "Display avatars of people currently looking at the same ticket, like a shared document.",
            TicketType.Improvement, TicketPriority.Low, "Frontend", Jordan, null, TicketStatus.WontDo, 14, [],
            "Not worth the complexity right now. Comments already cover collaboration."),
        new("Password reset email lands in spam",
            "Several reporters say the reset email goes to their spam folder (Gmail and Outlook).",
            TicketType.Bug, TicketPriority.High, "Infrastructure", Taylor, Sam, TicketStatus.InProgress, 2,
            [new(Sam, "Adding SPF and DKIM records for the sending domain. DNS changes take a few hours to propagate.")]),
        new("Export tickets to CSV",
            "Managers want to open the filtered ticket list in a spreadsheet. Export what the current filters show.",
            TicketType.Improvement, TicketPriority.Medium, "Backend", Jordan, Priya, TicketStatus.ToDo, 5, []),
        new("Keyboard shortcut to create a ticket",
            "Press C anywhere (outside text fields) to open the Create ticket form, like other trackers.",
            TicketType.Improvement, TicketPriority.Low, "Frontend", Taylor, null, TicketStatus.Backlog, 3, []),
        new("Sign-in broken in Safari",
            "I can't sign in using Safari. Nothing happens when I press the button.",
            TicketType.Bug, TicketPriority.High, "Frontend", Taylor, null, TicketStatus.WontDo, 3, [],
            "Duplicate of \"Login button does nothing on Safari 18\". Follow that ticket for updates."),
        new("Set up a staging environment",
            "A copy of production with its own database, deployed automatically from the main branch.",
            TicketType.Task, TicketPriority.Medium, "Infrastructure", Admin, Sam, TicketStatus.Done, 11,
            [new(Sam, "Staging is live and deploys on every merge to main.")]),
        new("Comment box loses text when switching tabs",
            "Typing a long comment, switching to the History tab and back clears the draft.",
            TicketType.Bug, TicketPriority.Medium, "Frontend", Jordan, Alex, TicketStatus.InReview, 4,
            [new(Alex, "Fix keeps the draft in memory while the ticket is open. Ready for review.")]),
    ];
}

# OpenTicket user guide

OpenTicket is an issue tracker for software teams: report work, plan it on a board, and follow it to done. This guide is also available inside the app under **Guide** in the sidebar.

## Try the demo

On a demo server, the sign-in page shows **Explore the live demo**. Pick a role and you are signed in. No account is needed.

| Role | You are | Good first things to try |
|---|---|---|
| **Developer** | Alex Rivera | Drag one of your In Progress tickets to In Review. Use "Assign to me" on an unassigned ticket. Change a ticket's priority, then check its History. |
| **Reporter** | Jordan Kim | Create a Bug and follow the hints in the form. Find your tickets on the board. Add a comment. |
| **Admin** | Morgan Lee | Assign an unassigned ticket to a developer. Drag any ticket on the board. Add a category or a user. |

- **Switching role.** Use **Switch role** in the yellow banner.
- **Resets.** Demo data goes back to its starting state every few hours, as the banner says.
- **What's closed in the demo.** Sign-up is turned off, and the demo accounts themselves can't be changed.

## Roles

- **Reporter.**
  - Creates tickets.
  - Sees and comments on their own tickets.
  - Edits a ticket while it is in Backlog or To Do.
  - Can close their own ticket as Won't Do before work starts, or reopen it.
- **Developer.**
  - Sees every ticket.
  - Picks up unassigned work with **Assign to me**.
  - Moves and edits the tickets assigned to them.
- **Admin.**
  - Moves and edits any ticket.
  - Assigns developers.
  - Manages users, roles and categories.

New accounts start as Reporters. An admin can promote them under **Users**.

## Workflow

```
Backlog → To Do → In Progress → In Review → Done
             any open status → Won't Do (reason required)
             Done / Won't Do → reopen to To Do
```

| Status | Meaning |
|---|---|
| Backlog | Reported, not planned yet. Every new ticket starts here. |
| To Do | Planned and ready to be picked up. |
| In Progress | A developer is working on it. Needs an assignee. |
| In Review | The work is being checked. Needs an assignee. |
| Done | Finished. |
| Won't Do | Closed without doing it, always with a reason. |

- **Moving open tickets.** Open tickets can move between any of the open statuses.
- **Menus only offer valid moves.** The status menu and the board only offer moves you are allowed to make.

## Types and priorities

- **Types:**
  - **Bug:** something is broken.
  - **Task:** work that isn't a feature.
  - **Story:** a feature from the user's point of view.
  - **Improvement:** making something better.
- **Priorities:**
  - **Urgent:** drop everything.
  - **High:** pick it up next.
  - **Medium:** normal.
  - **Low:** nice to have.

## Sprints

- **What a sprint is.** A sprint is a time box, usually two weeks, with a goal. Only one sprint is active at a time.
- **Planning.** Admins and developers plan on the **Backlog** page.
  - Drag tickets from the backlog into a sprint, or use the move button on a row.
  - Joining a sprint doesn't change a ticket's status; move it on the board when work starts.
- **Start sprint.** Asks for the dates (two weeks by default) and puts the sprint on the board.
- **Complete sprint.**
  - Asks where unfinished tickets go: the next planned sprint or the backlog.
  - Finished tickets stay with the completed sprint.
- **Sprint field.** Every ticket shows its sprint in the details panel. Staff can change it there.

## The board

- **What it shows.** During a sprint, the board shows only the active sprint, with its dates, days left, progress and goal. With no active sprint, it shows every ticket.

- **Drag a card** to another column to change its status. While you drag, columns you can't drop into are dimmed.
- **On a phone,** press and hold a card to pick it up. A normal swipe scrolls the board.
- **Filter** with Only mine, Unassigned, the type chips, the category picker or the search box.
- **Click a card** to open the ticket.

## Working on a ticket

- **Status button.** The coloured status button at the top right lists your possible moves. Won't Do asks for a reason.
- **Inline edits.** Type, priority and category are edited in the details panel. The pencil icon edits the summary and description.
- **Comments and History.** **Comments** hold the conversation. **History** records every change and who made it.
- **Closed tickets** are read-only. Reopen to To Do to continue.

## Signing in

- **Email and password.** Use the password field's eye icon to check what you typed.
- **Forgot password?** Shown when the server can send email. The link works once and expires after one hour. Using it signs you out on other devices.
- **Continue with GitHub / Google.** Shown when the server has them set up. If your verified email matches an existing account, the two are linked.
- **Change password** is in the account menu, top right.

## FAQ

- **I can't drag a card or I don't see a status in the menu.** You can only make moves your role allows. In Progress and In Review also need an assignee.
- **How do I find a ticket?** Search by words or by key, e.g. `OT-000042` (or just `42`).
- **The comment box is gone.** The ticket is closed. Reopen it to To Do.

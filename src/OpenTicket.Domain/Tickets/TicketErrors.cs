using OpenTicket.Domain.Common;

namespace OpenTicket.Domain.Tickets;

public static class TicketErrors
{
    public static readonly Error NotFound = Error.NotFound("ticket.not_found", "The ticket was not found.");

    public static readonly Error Closed =
        Error.BusinessRule("ticket.closed", "This ticket is closed. Reopen it to make changes.");

    public static readonly Error NotAllowed =
        Error.Forbidden("ticket.forbidden", "You are not allowed to perform this action on this ticket.");

    public static readonly Error ReporterEditOnlyBeforeWork = Error.BusinessRule(
        "ticket.not_editable", "You can edit your ticket only while it is in Backlog or To Do.");

    public static readonly Error NotAssigned = Error.BusinessRule(
        "ticket.not_assigned", "Assign the ticket to a developer before moving it to In Progress or In Review.");

    public static readonly Error AlreadyAssigned = Error.BusinessRule(
        "ticket.already_assigned", "This ticket is already assigned to someone else. Ask an admin to reassign it.");

    public static readonly Error UnassignWhileInProgress = Error.BusinessRule(
        "ticket.unassign_in_progress", "Move the ticket back to To Do before unassigning it.");

    public static readonly Error StaleVersion = Error.Conflict(
        "ticket.stale_version", "The ticket was changed by someone else. Reload it and try again.");

    public static readonly Error ReasonRequired = Error.Validation(
        "ticket.reason_required", "Say why this ticket won't be done.",
        new Dictionary<string, string[]> { ["reason"] = ["Say why this ticket won't be done."] });

    public static readonly Error CategoryInactive = Error.Validation(
        "ticket.category_invalid", "The selected category does not exist or is not active.",
        new Dictionary<string, string[]> { ["categoryId"] = ["The selected category does not exist or is not active."] });

    public static readonly Error AssigneeNotDeveloper = Error.Validation(
        "ticket.assignee_invalid", "Tickets can be assigned only to active developers.",
        new Dictionary<string, string[]> { ["assigneeId"] = ["Tickets can be assigned only to active developers."] });

    public static readonly Error ReopenToToDoOnly = Error.BusinessRule(
        "ticket.invalid_transition", "A closed ticket can only be reopened to To Do.");

    public static Error Blank(string field) => Error.Validation(
        "ticket.field_blank", $"The {field} cannot be blank.",
        new Dictionary<string, string[]> { [field] = [$"The {field} cannot be blank."] });

    public static readonly Error SprintClosed = Error.BusinessRule(
        "ticket.sprint_closed", "Tickets can only be planned into a planned or active sprint.");
}

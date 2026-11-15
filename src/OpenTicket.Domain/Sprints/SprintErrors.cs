using OpenTicket.Domain.Common;

namespace OpenTicket.Domain.Sprints;

public static class SprintErrors
{
    public static readonly Error NotFound = Error.NotFound("sprint.not_found", "The sprint was not found.");

    public static readonly Error NotAllowed =
        Error.Forbidden("sprint.forbidden", "Only admins and developers can plan sprints.");

    public static readonly Error AlreadyActive = Error.Conflict(
        "sprint.already_active", "Another sprint is already active. Complete it before starting a new one.");

    public static readonly Error NotPlanned =
        Error.BusinessRule("sprint.not_planned", "Only a planned sprint can be started or deleted.");

    public static readonly Error NotActive =
        Error.BusinessRule("sprint.not_active", "Only the active sprint can be completed.");

    public static readonly Error Completed =
        Error.BusinessRule("sprint.completed", "This sprint is completed and can no longer change.");

    public static readonly Error InvalidTarget = Error.Validation(
        "sprint.invalid_target", "Unfinished tickets can only move to a planned sprint or the backlog.",
        new Dictionary<string, string[]> { ["moveOpenTicketsTo"] = ["Choose a planned sprint or the backlog."] });

    public static readonly Error InvalidDates = Error.Validation(
        "sprint.invalid_dates", "The end date must be on or after the start date.",
        new Dictionary<string, string[]> { ["endDate"] = ["The end date must be on or after the start date."] });

    public static Error Blank(string field) => Error.Validation(
        "sprint.field_blank", $"The {field} cannot be blank.",
        new Dictionary<string, string[]> { [field] = [$"The {field} cannot be blank."] });
}

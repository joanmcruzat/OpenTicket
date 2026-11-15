namespace OpenTicket.Domain.Users;

/// <summary>Each user has exactly one role (docs/architecture.md §3.1). Names double as Identity role names.</summary>
public enum UserRole
{
    Admin = 0,
    Agent = 1,
    Customer = 2,
}

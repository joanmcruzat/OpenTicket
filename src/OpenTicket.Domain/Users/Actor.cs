namespace OpenTicket.Domain.Users;

/// <summary>The authenticated user performing an operation.</summary>
public sealed record Actor(Guid UserId, UserRole Role)
{
    public bool IsAdmin => Role == UserRole.Admin;

    public bool IsAgent => Role == UserRole.Agent;

    public bool IsCustomer => Role == UserRole.Customer;
}

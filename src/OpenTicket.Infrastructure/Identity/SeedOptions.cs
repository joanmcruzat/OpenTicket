namespace OpenTicket.Infrastructure.Identity;

/// <summary>First-admin bootstrap. Used only when no admin exists yet; values come from environment or user-secrets.</summary>
public sealed class SeedOptions
{
    public const string Section = "Seed";

    public string? AdminEmail { get; init; }

    public string? AdminPassword { get; init; }

    public string AdminDisplayName { get; init; } = "Administrator";
}

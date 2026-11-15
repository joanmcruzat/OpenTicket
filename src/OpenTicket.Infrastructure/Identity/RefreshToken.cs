namespace OpenTicket.Infrastructure.Identity;

/// <summary>A rotating refresh token. Only the SHA-256 hash of the token is stored.</summary>
public sealed class RefreshToken
{
    public const int TokenHashLength = 64;

    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid UserId { get; set; }

    public required string TokenHash { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? RevokedAtUtc { get; set; }

    public Guid? ReplacedById { get; set; }
}

using System.ComponentModel.DataAnnotations;

namespace OpenTicket.Infrastructure.Identity;

public sealed class JwtOptions
{
    public const string Section = "Jwt";

    [Required]
    public string Issuer { get; init; } = "openticket";

    [Required]
    public string Audience { get; init; } = "openticket";

    /// <summary>Base64-encoded HMAC key of at least 32 bytes. Supplied by environment or user-secrets only.</summary>
    [Required]
    public string SigningKey { get; init; } = string.Empty;

    [Range(1, 60)]
    public int AccessTokenMinutes { get; init; } = 15;

    [Range(1, 90)]
    public int RefreshTokenDays { get; init; } = 7;

    public byte[] GetSigningKeyBytes()
    {
        var buffer = new byte[SigningKey.Length];
        return Convert.TryFromBase64String(SigningKey, buffer, out var written) ? buffer[..written] : [];
    }

    public bool HasStrongSigningKey() => GetSigningKeyBytes().Length >= 32;
}

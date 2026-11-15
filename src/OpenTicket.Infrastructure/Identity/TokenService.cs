using System.Buffers.Text;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

using OpenTicket.Domain.Users;

namespace OpenTicket.Infrastructure.Identity;

public static class OpenTicketClaims
{
    public const string Role = "role";
    public const string Name = "name";
}

internal sealed class TokenService(IOptions<JwtOptions> options, TimeProvider clock)
{
    private readonly JwtOptions _options = options.Value;
    private readonly JsonWebTokenHandler _handler = new();

    public (string Token, DateTime ExpiresAtUtc) CreateAccessToken(ApplicationUser user, UserRole role)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var expires = now.AddMinutes(_options.AccessTokenMinutes);
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expires,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(OpenTicketClaims.Role, role.ToString()),
                new Claim(OpenTicketClaims.Name, user.DisplayName),
            ]),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(_options.GetSigningKeyBytes()), SecurityAlgorithms.HmacSha256),
        };
        return (_handler.CreateToken(descriptor), expires);
    }

    /// <summary>A 256-bit random, URL-safe token. Only its hash is persisted.</summary>
    public static string CreateRefreshToken() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    public static string HashRefreshToken(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}

using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ReservationSystem.Shared.Business.Infrastructure.Configuration;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace ReservationSystem.Shared.Business.Security;

/// <summary>
/// Generates signed JWT access tokens using HMAC-SHA256 and the configured
/// <see cref="JwtOptions"/>. Register as scoped in DI.
/// </summary>
public sealed class JwtService : IJwtService
{
    private readonly JwtOptions _options;

    public JwtService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
    }

    /// <inheritdoc />
    public (string Token, DateTime ExpiresAt) GenerateToken(IEnumerable<Claim> claims)
    {
        var keyBytes = Convert.FromBase64String(_options.Secret);
        var key = new SymmetricSecurityKey(keyBytes);
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiresAt = DateTime.UtcNow.AddMinutes(_options.AccessTokenExpiryMinutes);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}

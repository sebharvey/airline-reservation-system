using System.Security.Claims;

namespace ReservationSystem.Shared.Business.Security;

/// <summary>
/// Generates signed JWT access tokens using the configured <see cref="Infrastructure.Configuration.JwtOptions"/>.
/// Register as scoped in DI alongside <see cref="JwtService"/>.
/// </summary>
public interface IJwtService
{
    /// <summary>
    /// Creates a signed JWT containing the supplied claims.
    /// The issuer, audience, expiry, and signing key come from the bound
    /// <see cref="Infrastructure.Configuration.JwtOptions"/> configuration section.
    /// </summary>
    /// <param name="claims">Domain-specific claims to embed in the token.</param>
    /// <returns>The compact serialized token string and its UTC expiry time.</returns>
    (string Token, DateTime ExpiresAt) GenerateToken(IEnumerable<Claim> claims);
}

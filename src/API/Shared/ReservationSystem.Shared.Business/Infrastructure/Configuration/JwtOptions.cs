using System.ComponentModel.DataAnnotations;

namespace ReservationSystem.Shared.Business.Infrastructure.Configuration;

/// <summary>
/// Configuration for JWT access token generation and validation.
/// Bind from the "Jwt" section in appsettings or environment variables.
///
/// Each service that issues tokens configures its own issuer and audience
/// via application settings. The Secret must be a Base64-encoded 256-bit key.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>Base64-encoded 256-bit HMAC-SHA256 signing secret.</summary>
    [Required(ErrorMessage = "Jwt:Secret is required. Configure a Base64-encoded 256-bit signing key.")]
    [MinLength(1, ErrorMessage = "Jwt:Secret must not be empty.")]
    public string Secret { get; init; } = string.Empty;

    /// <summary>Identifies the principal that issued the token.</summary>
    public string Issuer { get; init; } = "apex-air-user";

    /// <summary>Identifies the recipients that the token is intended for.</summary>
    public string Audience { get; init; } = "apex-air-reservation";

    /// <summary>Access token lifetime in minutes.</summary>
    public int AccessTokenExpiryMinutes { get; init; } = 480;
}

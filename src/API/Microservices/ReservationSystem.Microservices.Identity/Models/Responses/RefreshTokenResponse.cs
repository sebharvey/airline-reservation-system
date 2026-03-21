namespace ReservationSystem.Microservices.Identity.Models.Responses;

/// <summary>
/// HTTP response body for POST /v1/auth/refresh.
/// </summary>
public sealed class RefreshTokenResponse
{
    public string AccessToken { get; init; } = string.Empty;
    public string RefreshToken { get; init; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; init; }
}

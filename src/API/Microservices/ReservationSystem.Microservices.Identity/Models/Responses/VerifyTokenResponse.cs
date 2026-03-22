namespace ReservationSystem.Microservices.Identity.Models.Responses;

/// <summary>
/// HTTP response body for POST /v1/auth/verify.
/// Returned when the access token is valid. A 401 is returned when invalid.
/// </summary>
public sealed class VerifyTokenResponse
{
    public bool Valid { get; init; }
    public Guid UserAccountId { get; init; }
    public string Email { get; init; } = string.Empty;
}

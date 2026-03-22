namespace ReservationSystem.Orchestration.Loyalty.Models.Requests;

/// <summary>
/// HTTP request body for POST /v1/auth/refresh.
/// </summary>
public sealed class RefreshTokenRequest
{
    public string RefreshToken { get; init; } = string.Empty;
}

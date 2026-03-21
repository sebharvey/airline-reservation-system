namespace ReservationSystem.Microservices.Identity.Models.Requests;

/// <summary>
/// HTTP request body for POST /v1/auth/refresh.
/// </summary>
public sealed class RefreshTokenRequest
{
    public string Token { get; init; } = string.Empty;
    public string? DeviceHint { get; init; }
}

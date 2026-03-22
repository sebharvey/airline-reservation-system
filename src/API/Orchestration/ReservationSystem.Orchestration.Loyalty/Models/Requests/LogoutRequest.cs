namespace ReservationSystem.Orchestration.Loyalty.Models.Requests;

/// <summary>
/// HTTP request body for POST /v1/auth/logout.
/// </summary>
public sealed class LogoutRequest
{
    public string RefreshToken { get; init; } = string.Empty;
}

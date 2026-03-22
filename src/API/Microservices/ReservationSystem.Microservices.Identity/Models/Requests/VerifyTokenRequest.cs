namespace ReservationSystem.Microservices.Identity.Models.Requests;

/// <summary>
/// HTTP request body for POST /v1/auth/verify.
/// </summary>
public sealed class VerifyTokenRequest
{
    public string AccessToken { get; init; } = string.Empty;
}

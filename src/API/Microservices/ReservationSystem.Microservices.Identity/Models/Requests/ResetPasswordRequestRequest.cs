namespace ReservationSystem.Microservices.Identity.Models.Requests;

/// <summary>
/// HTTP request body for POST /v1/auth/password/reset-request.
/// </summary>
public sealed class ResetPasswordRequestRequest
{
    public string Email { get; init; } = string.Empty;
}

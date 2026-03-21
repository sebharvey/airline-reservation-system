namespace ReservationSystem.Microservices.Identity.Models.Requests;

/// <summary>
/// HTTP request body for POST /v1/auth/password/reset.
/// </summary>
public sealed class ResetPasswordRequest
{
    public string ResetToken { get; init; } = string.Empty;
    public string NewPassword { get; init; } = string.Empty;
}

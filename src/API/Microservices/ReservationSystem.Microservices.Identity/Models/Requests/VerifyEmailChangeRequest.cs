namespace ReservationSystem.Microservices.Identity.Models.Requests;

/// <summary>
/// HTTP request body for POST /v1/email/verify.
/// </summary>
public sealed class VerifyEmailChangeRequest
{
    public string Token { get; init; } = string.Empty;
}

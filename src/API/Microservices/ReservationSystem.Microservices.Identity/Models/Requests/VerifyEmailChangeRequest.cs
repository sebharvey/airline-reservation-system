namespace ReservationSystem.Microservices.Identity.Models.Requests;

/// <summary>
/// HTTP request body for POST /v1/email/verify.
/// </summary>
public sealed class VerifyEmailChangeRequest
{
    public string VerificationToken { get; init; } = string.Empty;
}

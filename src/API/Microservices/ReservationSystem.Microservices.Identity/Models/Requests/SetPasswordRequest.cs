namespace ReservationSystem.Microservices.Identity.Models.Requests;

/// <summary>Request body for POST /v1/accounts/{userAccountId}/set-password.</summary>
public sealed class SetPasswordRequest
{
    public string? NewPassword { get; init; }
}

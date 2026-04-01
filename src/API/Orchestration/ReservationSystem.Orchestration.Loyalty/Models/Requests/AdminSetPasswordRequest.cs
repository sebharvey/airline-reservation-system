namespace ReservationSystem.Orchestration.Loyalty.Models.Requests;

/// <summary>Request body for POST /v1/admin/customers/{loyaltyNumber}/identity/set-password.</summary>
public sealed class AdminSetPasswordRequest
{
    public string? NewPassword { get; init; }
}

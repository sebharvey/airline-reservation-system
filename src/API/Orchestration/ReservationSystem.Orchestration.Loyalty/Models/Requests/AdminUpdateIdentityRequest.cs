namespace ReservationSystem.Orchestration.Loyalty.Models.Requests;

/// <summary>
/// Request body for PATCH /v1/admin/customers/{loyaltyNumber}/identity.
/// All fields are optional — only non-null values are applied.
/// </summary>
public sealed class AdminUpdateIdentityRequest
{
    /// <summary>New email address applied without a verification step.</summary>
    public string? Email { get; init; }

    /// <summary>When provided, sets the account locked/unlocked state directly.</summary>
    public bool? IsLocked { get; init; }
}

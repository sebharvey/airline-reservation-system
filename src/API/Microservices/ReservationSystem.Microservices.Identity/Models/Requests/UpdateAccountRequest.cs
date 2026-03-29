namespace ReservationSystem.Microservices.Identity.Models.Requests;

/// <summary>
/// Request body for the admin PATCH /v1/accounts/{userAccountId} endpoint.
/// All fields are optional — only non-null values are applied.
/// </summary>
public sealed class UpdateAccountRequest
{
    /// <summary>New email address. Applied directly without a verification step (admin override).</summary>
    public string? Email { get; init; }

    /// <summary>When provided, sets the account locked state directly.</summary>
    public bool? IsLocked { get; init; }
}

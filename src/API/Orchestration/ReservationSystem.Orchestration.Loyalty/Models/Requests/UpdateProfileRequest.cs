namespace ReservationSystem.Orchestration.Loyalty.Models.Requests;

/// <summary>
/// Request body for PATCH /v1/customers/{loyaltyNumber}/profile.
/// Fields identityId, tier, active, and points are not permitted and are ignored.
/// </summary>
public sealed class UpdateProfileRequest
{
    public string? GivenName { get; init; }
    public string? Surname { get; init; }
    public DateOnly? DateOfBirth { get; init; }
    public string? Nationality { get; init; }
    public string? PhoneNumber { get; init; }
    public string? PreferredLanguage { get; init; }
}

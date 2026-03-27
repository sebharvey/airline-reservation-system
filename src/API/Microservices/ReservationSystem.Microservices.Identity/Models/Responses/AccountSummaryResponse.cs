namespace ReservationSystem.Microservices.Identity.Models.Responses;

/// <summary>
/// HTTP response body for a user account summary — exposes only non-sensitive fields.
/// </summary>
public sealed class AccountSummaryResponse
{
    public Guid UserAccountId { get; init; }
    public string Email { get; init; } = string.Empty;
    public bool IsEmailVerified { get; init; }
}

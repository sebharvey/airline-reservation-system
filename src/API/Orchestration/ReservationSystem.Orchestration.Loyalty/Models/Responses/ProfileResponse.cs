namespace ReservationSystem.Orchestration.Loyalty.Models.Responses;

public sealed class ProfileResponse
{
    public string LoyaltyNumber { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string? PhoneNumber { get; init; }
    public DateOnly? DateOfBirth { get; init; }
    public string Tier { get; init; } = string.Empty;
    public decimal PointsBalance { get; init; }
    public DateTime MemberSince { get; init; }
}

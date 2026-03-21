namespace ReservationSystem.Microservices.Customer.Application.ReversePoints;

/// <summary>
/// Command carrying the data needed to reverse a points authorisation hold.
/// </summary>
public sealed record ReversePointsCommand(
    string LoyaltyNumber,
    string RedemptionReference,
    string? Reason);

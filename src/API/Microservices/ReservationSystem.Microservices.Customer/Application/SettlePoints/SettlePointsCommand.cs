namespace ReservationSystem.Microservices.Customer.Application.SettlePoints;

/// <summary>
/// Command carrying the data needed to settle points for a Customer.
/// </summary>
public sealed record SettlePointsCommand(
    string LoyaltyNumber,
    int Points,
    string? BookingReference,
    string? FlightNumber,
    string Description);

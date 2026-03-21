namespace ReservationSystem.Microservices.Customer.Application.ReinstatePoints;

/// <summary>
/// Command carrying the data needed to reinstate points for a Customer.
/// </summary>
public sealed record ReinstatePointsCommand(
    string LoyaltyNumber,
    int Points,
    string? BookingReference,
    string? FlightNumber,
    string Description);

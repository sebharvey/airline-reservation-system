namespace ReservationSystem.Microservices.Customer.Application.ReversePoints;

/// <summary>
/// Command carrying the data needed to reverse points for a Customer.
/// </summary>
public sealed record ReversePointsCommand(
    string LoyaltyNumber,
    int Points,
    string? BookingReference,
    string? FlightNumber,
    string Description);

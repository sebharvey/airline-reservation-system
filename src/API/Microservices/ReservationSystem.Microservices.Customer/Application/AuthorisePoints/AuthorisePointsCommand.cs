namespace ReservationSystem.Microservices.Customer.Application.AuthorisePoints;

/// <summary>
/// Command carrying the data needed to authorise points for a Customer.
/// </summary>
public sealed record AuthorisePointsCommand(
    string LoyaltyNumber,
    int Points,
    string? BookingReference,
    string? FlightNumber,
    string Description);

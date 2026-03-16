namespace ReservationSystem.Microservices.Offer.Application.CreateOffer;

/// <summary>
/// Command carrying the data needed to create a new Offer.
/// Immutable record — the application layer maps HTTP request models to this
/// before passing it to the handler, keeping the handler free of HTTP concerns.
/// </summary>
public sealed record CreateOfferCommand(
    string FlightNumber,
    string Origin,
    string Destination,
    DateTimeOffset DepartureAt,
    string FareClass,
    decimal TotalPrice,
    string Currency,
    string BaggageAllowance,
    bool IsRefundable,
    bool IsChangeable,
    int SeatsRemaining);

namespace ReservationSystem.Microservices.Seat.Application.CreateSeatPricing;

/// <summary>
/// Command carrying the data needed to create a new seat pricing rule.
/// </summary>
public sealed record CreateSeatPricingCommand(
    string CabinCode,
    string SeatPosition,
    string CurrencyCode,
    decimal Price,
    DateTime ValidFrom,
    DateTime? ValidTo);

namespace ReservationSystem.Microservices.Ancillary.Application.Seat.CreateSeatPricing;

/// <summary>
/// Command carrying the data needed to create a new seat pricing rule.
/// </summary>
public sealed record CreateSeatPricingCommand(
    string CabinCode,
    string Description,
    int Sequence,
    string CurrencyCode,
    decimal Price,
    DateTime ValidFrom,
    DateTime? ValidTo);

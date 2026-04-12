namespace ReservationSystem.Microservices.Ancillary.Application.Seat.UpdateSeatPricing;

/// <summary>
/// Command carrying the data needed to update an existing seat pricing rule.
/// </summary>
public sealed record UpdateSeatPricingCommand(
    Guid SeatPricingId,
    string? CabinCode,
    string? Description,
    int? Sequence,
    string? CurrencyCode,
    decimal? Price,
    bool? IsActive,
    DateTime? ValidFrom,
    DateTime? ValidTo);

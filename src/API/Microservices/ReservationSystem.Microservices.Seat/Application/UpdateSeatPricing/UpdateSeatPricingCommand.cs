namespace ReservationSystem.Microservices.Seat.Application.UpdateSeatPricing;

/// <summary>
/// Command carrying the data needed to update an existing seat pricing rule.
/// </summary>
public sealed record UpdateSeatPricingCommand(
    Guid SeatPricingId,
    decimal Price,
    bool IsActive,
    DateTimeOffset ValidFrom,
    DateTimeOffset? ValidTo);

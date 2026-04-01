namespace ReservationSystem.Microservices.Ancillary.Application.Seat.GetSeatPricing;

/// <summary>
/// Query carrying the pricing rule identifier needed to retrieve a single seat pricing rule.
/// </summary>
public sealed record GetSeatPricingQuery(Guid SeatPricingId);

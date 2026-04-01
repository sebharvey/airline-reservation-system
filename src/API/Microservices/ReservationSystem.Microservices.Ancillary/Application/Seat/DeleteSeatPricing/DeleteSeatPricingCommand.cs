namespace ReservationSystem.Microservices.Ancillary.Application.Seat.DeleteSeatPricing;

/// <summary>
/// Command carrying the identifier needed to delete a seat pricing rule.
/// </summary>
public sealed record DeleteSeatPricingCommand(Guid SeatPricingId);

namespace ReservationSystem.Microservices.Seat.Application.DeleteSeatPricing;

/// <summary>
/// Command carrying the identifier needed to delete a seat pricing rule.
/// </summary>
public sealed record DeleteSeatPricingCommand(Guid SeatPricingId);

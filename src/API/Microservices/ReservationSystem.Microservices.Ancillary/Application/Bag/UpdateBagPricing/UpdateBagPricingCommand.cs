namespace ReservationSystem.Microservices.Ancillary.Application.Bag.UpdateBagPricing;

public sealed record UpdateBagPricingCommand(
    Guid PricingId,
    decimal Price,
    DateTime ValidFrom,
    DateTime? ValidTo,
    bool IsActive);

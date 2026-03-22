namespace ReservationSystem.Microservices.Bags.Application.UpdateBagPricing;

public sealed record UpdateBagPricingCommand(
    Guid PricingId,
    decimal Price,
    DateTime ValidFrom,
    DateTime? ValidTo,
    bool IsActive);

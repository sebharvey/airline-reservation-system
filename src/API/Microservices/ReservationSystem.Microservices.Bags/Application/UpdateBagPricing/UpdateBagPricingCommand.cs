namespace ReservationSystem.Microservices.Bags.Application.UpdateBagPricing;

public sealed record UpdateBagPricingCommand(
    Guid PricingId,
    decimal Price,
    DateTimeOffset ValidFrom,
    DateTimeOffset? ValidTo,
    bool IsActive);

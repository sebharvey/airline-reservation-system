namespace ReservationSystem.Microservices.Bags.Application.UpdateBagPricing;

public sealed record UpdateBagPricingCommand(
    Guid PricingId,
    decimal Price,
    bool IsActive,
    DateTimeOffset ValidFrom,
    DateTimeOffset? ValidTo);

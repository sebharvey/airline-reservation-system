namespace ReservationSystem.Microservices.Ancillary.Application.Bag.UpdateBagPricing;

public sealed record UpdateBagPricingCommand(
    Guid PricingId,
    int BagSequence,
    string CurrencyCode,
    decimal Price,
    DateTime ValidFrom,
    DateTime? ValidTo,
    bool IsActive);

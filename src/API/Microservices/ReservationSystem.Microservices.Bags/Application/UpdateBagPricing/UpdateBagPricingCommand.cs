namespace ReservationSystem.Microservices.Bags.Application.UpdateBagPricing;

public sealed record UpdateBagPricingCommand(
    Guid PricingId,
    string CabinCode,
    int BagNumber,
    decimal Price,
    string Currency,
    bool IsActive);

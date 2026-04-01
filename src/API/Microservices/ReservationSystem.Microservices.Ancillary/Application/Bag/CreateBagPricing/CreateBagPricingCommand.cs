namespace ReservationSystem.Microservices.Ancillary.Application.Bag.CreateBagPricing;

public sealed record CreateBagPricingCommand(
    int BagSequence,
    string CurrencyCode,
    decimal Price,
    DateTime ValidFrom,
    DateTime? ValidTo);

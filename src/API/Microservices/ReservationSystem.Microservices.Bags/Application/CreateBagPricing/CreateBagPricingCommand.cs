namespace ReservationSystem.Microservices.Bags.Application.CreateBagPricing;

public sealed record CreateBagPricingCommand(
    int BagSequence,
    string CurrencyCode,
    decimal Price,
    DateTime ValidFrom,
    DateTime? ValidTo);

namespace ReservationSystem.Microservices.Bags.Application.CreateBagPricing;

public sealed record CreateBagPricingCommand(
    int BagSequence,
    decimal Price,
    string CurrencyCode,
    DateTimeOffset ValidFrom,
    DateTimeOffset? ValidTo);

namespace ReservationSystem.Microservices.Bags.Application.CreateBagPricing;

public sealed record CreateBagPricingCommand(
    string CabinCode,
    int BagNumber,
    decimal Price,
    string Currency);

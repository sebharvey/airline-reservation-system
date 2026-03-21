namespace ReservationSystem.Orchestration.Retail.Application.CreateBasket;

public sealed record CreateBasketCommand(
    string CustomerId,
    string? LoyaltyNumber);

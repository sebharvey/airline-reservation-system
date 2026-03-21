namespace ReservationSystem.Microservices.Order.Application.CreateBasket;

public sealed record CreateBasketCommand(
    string ChannelCode,
    string CurrencyCode,
    string BookingType,
    string? LoyaltyNumber,
    int? TotalPointsAmount);

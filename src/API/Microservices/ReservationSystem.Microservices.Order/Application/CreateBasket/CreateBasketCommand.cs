namespace ReservationSystem.Microservices.Order.Application.CreateBasket;

/// <summary>
/// Command carrying the data needed to create a new shopping basket.
/// </summary>
public sealed record CreateBasketCommand(
    string ChannelCode,
    string CurrencyCode,
    DateTimeOffset ExpiresAt);

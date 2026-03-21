namespace ReservationSystem.Microservices.Order.Application.ExpireBasket;

/// <summary>
/// Command to expire an open basket, preventing further modifications.
/// </summary>
public sealed record ExpireBasketCommand(Guid BasketId);

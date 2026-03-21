namespace ReservationSystem.Microservices.Order.Application.GetBasket;

/// <summary>
/// Query to retrieve a basket by its unique identifier.
/// </summary>
public sealed record GetBasketQuery(Guid BasketId);

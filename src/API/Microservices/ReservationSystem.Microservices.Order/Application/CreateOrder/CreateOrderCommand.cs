namespace ReservationSystem.Microservices.Order.Application.CreateOrder;

/// <summary>
/// Command to confirm a basket and create a new order (booking).
/// </summary>
public sealed record CreateOrderCommand(Guid BasketId);

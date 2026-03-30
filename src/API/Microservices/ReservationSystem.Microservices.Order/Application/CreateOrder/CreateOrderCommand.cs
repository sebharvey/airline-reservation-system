namespace ReservationSystem.Microservices.Order.Application.CreateOrder;

public sealed record CreateOrderCommand(
    Guid BasketId,
    string? RedemptionReference,
    string BookingType);

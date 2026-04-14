namespace ReservationSystem.Microservices.Order.Application.CreateOrder;

public sealed record CreateOrderCommand(
    Guid BasketId,
    string ChannelCode,
    string? RedemptionReference,
    string BookingType);

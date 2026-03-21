namespace ReservationSystem.Microservices.Order.Application.CreateOrder;

public sealed record CreateOrderCommand(
    Guid BasketId,
    string ETicketsJson,
    string PaymentReferencesJson,
    string? RedemptionReference,
    string BookingType);

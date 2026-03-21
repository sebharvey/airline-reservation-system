namespace ReservationSystem.Microservices.Order.Application.CancelOrder;

public sealed record CancelOrderCommand(
    string BookingReference,
    string RequestBody);

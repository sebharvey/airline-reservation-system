namespace ReservationSystem.Microservices.Order.Application.CancelOrder;

/// <summary>
/// Command to cancel a confirmed order.
/// </summary>
public sealed record CancelOrderCommand(string BookingReference);

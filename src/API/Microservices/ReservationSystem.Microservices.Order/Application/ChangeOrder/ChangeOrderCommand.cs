namespace ReservationSystem.Microservices.Order.Application.ChangeOrder;

/// <summary>
/// Command to change flight details on a confirmed order.
/// </summary>
public sealed record ChangeOrderCommand(string BookingReference, string ChangeData);

namespace ReservationSystem.Microservices.Order.Application.UpdateOrderPassengers;

/// <summary>
/// Command to update passenger contact and personal details on a confirmed order.
/// </summary>
public sealed record UpdateOrderPassengersCommand(string BookingReference, string PassengersData);

namespace ReservationSystem.Microservices.Order.Application.UpdateOrderBags;

/// <summary>
/// Command to add bag ancillaries to a confirmed order post-booking.
/// </summary>
public sealed record UpdateOrderBagsCommand(string BookingReference, string BagsData);

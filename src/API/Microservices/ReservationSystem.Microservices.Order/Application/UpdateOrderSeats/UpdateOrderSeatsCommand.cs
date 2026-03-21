namespace ReservationSystem.Microservices.Order.Application.UpdateOrderSeats;

/// <summary>
/// Command to update seat assignments on a confirmed order post-booking.
/// </summary>
public sealed record UpdateOrderSeatsCommand(string BookingReference, string SeatsData);

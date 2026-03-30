namespace ReservationSystem.Microservices.Order.Application.UpdateOrderETickets;

public sealed record UpdateOrderETicketsCommand(string BookingReference, string ETicketsJson);

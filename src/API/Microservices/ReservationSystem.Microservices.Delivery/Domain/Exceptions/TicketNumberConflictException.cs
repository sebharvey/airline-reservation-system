namespace ReservationSystem.Microservices.Delivery.Domain.Exceptions;

public sealed class TicketNumberConflictException : Exception
{
    public TicketNumberConflictException(long ticketNumber)
        : base($"Ticket number {ticketNumber} is already taken.") { }
}

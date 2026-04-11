namespace ReservationSystem.Microservices.Delivery.Domain.Exceptions;

public sealed class TicketNumberConflictException : Exception
{
    public TicketNumberConflictException(string eTicketNumber)
        : base($"Ticket number {eTicketNumber} is already taken.") { }
}

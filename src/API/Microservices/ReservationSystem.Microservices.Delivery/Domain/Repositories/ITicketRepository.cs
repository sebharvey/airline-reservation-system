using ReservationSystem.Microservices.Delivery.Domain.Entities;

namespace ReservationSystem.Microservices.Delivery.Domain.Repositories;

public interface ITicketRepository
{
    Task<Ticket?> GetByIdAsync(Guid ticketId, CancellationToken cancellationToken = default);

    Task<Ticket?> GetByETicketNumberAsync(string eTicketNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a ticket by e-ticket number including its <see cref="TicketTax"/> breakdown
    /// (required for <c>GetAttributedValue</c>).
    /// </summary>
    Task<Ticket?> GetByETicketNumberWithTaxesAsync(string eTicketNumber, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Ticket>> GetByBookingReferenceAsync(string bookingReference, CancellationToken cancellationToken = default);

    Task CreateAsync(Ticket ticket, CancellationToken cancellationToken = default);

    Task UpdateAsync(Ticket ticket, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all seat numbers already assigned to passengers on the given flight
    /// departing from <paramref name="origin"/>. Used to avoid conflicts during OLCI auto-assignment.
    /// </summary>
    Task<IReadOnlyList<string>> GetAssignedSeatsForFlightAsync(string flightNumber, string origin, CancellationToken cancellationToken = default);
}

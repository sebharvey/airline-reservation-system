using ReservationSystem.Microservices.Delivery.Domain.Entities;

namespace ReservationSystem.Microservices.Delivery.Domain.Repositories;

public interface ITicketRepository
{
    Task<Ticket?> GetByIdAsync(Guid ticketId, CancellationToken cancellationToken = default);

    Task<Ticket?> GetByETicketNumberAsync(string eTicketNumber, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Ticket>> GetByBookingReferenceAsync(string bookingReference, CancellationToken cancellationToken = default);

    Task CreateAsync(Ticket ticket, CancellationToken cancellationToken = default);

    Task UpdateAsync(Ticket ticket, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the highest numeric sequence already issued (the trailing digits of ETicketNumber).
    /// Returns 0 when no tickets exist. Used by handlers to derive the next candidate number;
    /// callers must retry on <see cref="Exceptions.TicketNumberConflictException"/> in case of
    /// a concurrent insert.
    /// </summary>
    Task<long> GetMaxTicketSequenceAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all seat numbers already assigned to passengers on the given flight
    /// departing from <paramref name="origin"/>. Used to avoid conflicts during OLCI auto-assignment.
    /// </summary>
    Task<IReadOnlyList<string>> GetAssignedSeatsForFlightAsync(string flightNumber, string origin, CancellationToken cancellationToken = default);
}

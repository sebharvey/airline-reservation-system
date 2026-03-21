using ReservationSystem.Microservices.Delivery.Domain.Entities;

namespace ReservationSystem.Microservices.Delivery.Domain.Repositories;

public interface ITicketRepository
{
    Task<Ticket?> GetByIdAsync(Guid ticketId, CancellationToken cancellationToken = default);

    Task<Ticket?> GetByETicketNumberAsync(string eTicketNumber, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Ticket>> GetByBookingReferenceAsync(string bookingReference, CancellationToken cancellationToken = default);

    Task CreateAsync(Ticket ticket, CancellationToken cancellationToken = default);

    Task UpdateAsync(Ticket ticket, CancellationToken cancellationToken = default);

    Task<int> GetTicketCountAsync(CancellationToken cancellationToken = default);
}

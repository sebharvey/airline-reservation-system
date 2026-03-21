using ReservationSystem.Microservices.Delivery.Domain.Entities;

namespace ReservationSystem.Microservices.Delivery.Domain.Repositories;

/// <summary>
/// Port (interface) for Ticket persistence.
/// Defined in Domain so the Application layer can depend on it without
/// taking a dependency on Infrastructure.
/// </summary>
public interface ITicketRepository
{
    Task<Ticket?> GetByIdAsync(Guid ticketId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Ticket>> GetByManifestIdAsync(Guid manifestId, CancellationToken cancellationToken = default);

    Task CreateAsync(Ticket ticket, CancellationToken cancellationToken = default);

    Task UpdateAsync(Ticket ticket, CancellationToken cancellationToken = default);
}

using ReservationSystem.Microservices.Delivery.Domain.Entities;

namespace ReservationSystem.Microservices.Delivery.Domain.Repositories;

public interface IDocumentRepository
{
    Task<Document?> GetByIdAsync(Guid documentId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Document>> GetByBookingReferenceAsync(string bookingReference, CancellationToken cancellationToken = default);

    Task CreateAsync(Document document, CancellationToken cancellationToken = default);

    Task<int> GetDocumentCountAsync(CancellationToken cancellationToken = default);
}

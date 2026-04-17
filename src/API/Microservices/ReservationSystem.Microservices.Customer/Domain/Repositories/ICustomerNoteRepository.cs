using ReservationSystem.Microservices.Customer.Domain.Entities;

namespace ReservationSystem.Microservices.Customer.Domain.Repositories;

public interface ICustomerNoteRepository
{
    Task<IReadOnlyList<CustomerNote>> GetByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default);
    Task<CustomerNote?> GetByIdAsync(Guid noteId, CancellationToken cancellationToken = default);
    Task AddAsync(CustomerNote note, CancellationToken cancellationToken = default);
    Task UpdateAsync(CustomerNote note, CancellationToken cancellationToken = default);
    Task DeleteAsync(CustomerNote note, CancellationToken cancellationToken = default);
}

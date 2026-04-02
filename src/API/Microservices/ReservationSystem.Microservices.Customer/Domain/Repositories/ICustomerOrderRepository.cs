using ReservationSystem.Microservices.Customer.Domain.Entities;

namespace ReservationSystem.Microservices.Customer.Domain.Repositories;

public interface ICustomerOrderRepository
{
    Task AddAsync(CustomerOrder order, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CustomerOrder>> GetByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid orderId, CancellationToken cancellationToken = default);
}

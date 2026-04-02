using Microsoft.EntityFrameworkCore;
using ReservationSystem.Microservices.Customer.Domain.Entities;
using ReservationSystem.Microservices.Customer.Domain.Repositories;

namespace ReservationSystem.Microservices.Customer.Infrastructure.Persistence;

public sealed class EfCustomerOrderRepository : ICustomerOrderRepository
{
    private readonly CustomerDbContext _db;

    public EfCustomerOrderRepository(CustomerDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(CustomerOrder order, CancellationToken cancellationToken = default)
    {
        _db.CustomerOrders.Add(order);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CustomerOrder>> GetByCustomerIdAsync(
        Guid customerId, CancellationToken cancellationToken = default)
    {
        return await _db.CustomerOrders
            .Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        return await _db.CustomerOrders
            .AnyAsync(o => o.OrderId == orderId, cancellationToken);
    }
}

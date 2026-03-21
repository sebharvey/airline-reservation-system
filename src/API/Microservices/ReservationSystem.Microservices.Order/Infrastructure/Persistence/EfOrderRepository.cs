using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Order.Domain.Repositories;

namespace ReservationSystem.Microservices.Order.Infrastructure.Persistence;

/// <summary>
/// Entity Framework Core implementation of <see cref="IOrderRepository"/>.
/// </summary>
public sealed class EfOrderRepository : IOrderRepository
{
    private readonly OrderDbContext _context;
    private readonly ILogger<EfOrderRepository> _logger;

    public EfOrderRepository(
        OrderDbContext context,
        ILogger<EfOrderRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Domain.Entities.Order?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.OrderId == orderId, cancellationToken);
    }

    public async Task<Domain.Entities.Order?> GetByBookingReferenceAsync(string bookingReference, CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.BookingReference == bookingReference, cancellationToken);
    }

    public async Task CreateAsync(Domain.Entities.Order order, CancellationToken cancellationToken = default)
    {
        _context.Orders.Add(order);
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogDebug("Inserted Order {OrderId} into [order].[Order]", order.OrderId);
    }

    public async Task UpdateAsync(Domain.Entities.Order order, CancellationToken cancellationToken = default)
    {
        _context.Orders.Update(order);
        var rowsAffected = await _context.SaveChangesAsync(cancellationToken);
        if (rowsAffected == 0)
            _logger.LogWarning("UpdateAsync found no row for Order {OrderId}", order.OrderId);
    }
}

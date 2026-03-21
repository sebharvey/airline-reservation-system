using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Order.Domain.Repositories;
using System.Text.Json;

namespace ReservationSystem.Microservices.Order.Infrastructure.Persistence;

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

    public async Task<IReadOnlyList<Domain.Entities.Order>> GetByFlightAsync(
        string flightNumber, string departureDate, string? status = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Orders.AsNoTracking().AsQueryable();

        if (!string.IsNullOrEmpty(status))
            query = query.Where(o => o.OrderStatus == status);

        var allOrders = await query.ToListAsync(cancellationToken);

        return allOrders.Where(o =>
        {
            try
            {
                using var doc = JsonDocument.Parse(o.OrderData);
                if (!doc.RootElement.TryGetProperty("dataLists", out var dataLists))
                    return false;
                if (!dataLists.TryGetProperty("flightSegments", out var segments))
                    return false;

                foreach (var segment in segments.EnumerateArray())
                {
                    var fn = segment.TryGetProperty("flightNumber", out var fnProp) ? fnProp.GetString() : null;
                    var dd = segment.TryGetProperty("departureDate", out var ddProp) ? ddProp.GetString() : null;

                    if (fn == flightNumber && dd == departureDate)
                        return true;
                }
            }
            catch { }
            return false;
        }).ToList().AsReadOnly();
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

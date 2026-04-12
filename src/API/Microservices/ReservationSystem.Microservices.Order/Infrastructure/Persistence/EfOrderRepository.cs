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

    public async Task<IReadOnlyDictionary<Guid, string?>> GetBookingReferencesByIdsAsync(
        IReadOnlyList<Guid> orderIds, CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .AsNoTracking()
            .Where(o => orderIds.Contains(o.OrderId))
            .ToDictionaryAsync(o => o.OrderId, o => o.BookingReference, cancellationToken);
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

    public async Task<IReadOnlyList<Domain.Entities.Order>> GetRecentAsync(int limit, CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .AsNoTracking()
            .OrderByDescending(o => o.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task CreateAsync(Domain.Entities.Order order, CancellationToken cancellationToken = default)
    {
        _context.Orders.Add(order);
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogDebug("Inserted Order {OrderId} into [order].[Order]", order.OrderId);
    }

    public async Task UpdateAsync(Domain.Entities.Order order, CancellationToken cancellationToken = default)
    {
        var existing = await _context.Orders.FindAsync([order.OrderId], cancellationToken);
        if (existing is null)
        {
            _logger.LogWarning("UpdateAsync found no row for Order {OrderId}", order.OrderId);
            return;
        }

        _context.Entry(existing).CurrentValues.SetValues(order);
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogDebug("Updated Order {OrderId} in [order].[Order]", order.OrderId);
    }

    public async Task<bool> DeleteDraftOrderAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var order = await _context.Orders
            .FirstOrDefaultAsync(o => o.OrderId == orderId && o.OrderStatus == "Draft", cancellationToken);

        if (order is null)
            return false;

        _context.Orders.Remove(order);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Deleted draft order {OrderId} from [order].[Order]", orderId);

        return true;
    }

    public async Task<int> DeleteExpiredDraftOrdersAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow.AddHours(-24);
        var expiredDrafts = await _context.Orders
            .Where(o => o.OrderStatus == "Draft" && o.UpdatedAt < cutoff)
            .ToListAsync(cancellationToken);

        if (expiredDrafts.Count == 0)
            return 0;

        _context.Orders.RemoveRange(expiredDrafts);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Deleted {Count} expired draft order(s) from [order].[Order]", expiredDrafts.Count);

        return expiredDrafts.Count;
    }
}

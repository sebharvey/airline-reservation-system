using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Order.Domain.Entities;
using ReservationSystem.Microservices.Order.Domain.Repositories;

namespace ReservationSystem.Microservices.Order.Infrastructure.Persistence;

/// <summary>
/// Entity Framework Core implementation of <see cref="IBasketRepository"/>.
/// </summary>
public sealed class EfBasketRepository : IBasketRepository
{
    private readonly OrderDbContext _context;
    private readonly ILogger<EfBasketRepository> _logger;

    public EfBasketRepository(
        OrderDbContext context,
        ILogger<EfBasketRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Basket?> GetByIdAsync(Guid basketId, CancellationToken cancellationToken = default)
    {
        return await _context.Baskets
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.BasketId == basketId, cancellationToken);
    }

    public async Task CreateAsync(Basket basket, CancellationToken cancellationToken = default)
    {
        _context.Baskets.Add(basket);
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogDebug("Inserted Basket {BasketId} into [order].[Basket]", basket.BasketId);
    }

    public async Task UpdateAsync(Basket basket, CancellationToken cancellationToken = default)
    {
        _context.Baskets.Update(basket);
        var rowsAffected = await _context.SaveChangesAsync(cancellationToken);
        if (rowsAffected == 0)
            _logger.LogWarning("UpdateAsync found no row for Basket {BasketId}", basket.BasketId);
    }

    public async Task DeleteAsync(Guid basketId, CancellationToken cancellationToken = default)
    {
        var basket = await _context.Baskets.FindAsync([basketId], cancellationToken);
        if (basket is not null)
        {
            _context.Baskets.Remove(basket);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}

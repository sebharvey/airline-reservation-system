using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Seat.Domain.Entities;
using ReservationSystem.Microservices.Seat.Domain.Repositories;

namespace ReservationSystem.Microservices.Seat.Infrastructure.Persistence;

/// <summary>
/// Entity Framework Core implementation of <see cref="ISeatPricingRepository"/>.
/// </summary>
public sealed class EfSeatPricingRepository : ISeatPricingRepository
{
    private readonly SeatDbContext _context;
    private readonly ILogger<EfSeatPricingRepository> _logger;

    public EfSeatPricingRepository(
        SeatDbContext context,
        ILogger<EfSeatPricingRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public Task<SeatPricing?> GetByIdAsync(Guid seatPricingId, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<IReadOnlyList<SeatPricing>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task CreateAsync(SeatPricing seatPricing, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task UpdateAsync(SeatPricing seatPricing, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task DeleteAsync(Guid seatPricingId, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}

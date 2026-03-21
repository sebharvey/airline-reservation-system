using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Seat.Domain.Entities;
using ReservationSystem.Microservices.Seat.Domain.Repositories;

namespace ReservationSystem.Microservices.Seat.Infrastructure.Persistence;

/// <summary>
/// Entity Framework Core implementation of <see cref="ISeatmapRepository"/>.
/// </summary>
public sealed class EfSeatmapRepository : ISeatmapRepository
{
    private readonly SeatDbContext _context;
    private readonly ILogger<EfSeatmapRepository> _logger;

    public EfSeatmapRepository(
        SeatDbContext context,
        ILogger<EfSeatmapRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public Task<Seatmap?> GetByIdAsync(Guid seatmapId, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<Seatmap?> GetActiveByAircraftTypeCodeAsync(string aircraftTypeCode, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task CreateAsync(Seatmap seatmap, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task UpdateAsync(Seatmap seatmap, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}

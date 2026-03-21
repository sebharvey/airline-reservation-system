using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Seat.Domain.Entities;
using ReservationSystem.Microservices.Seat.Domain.Repositories;

namespace ReservationSystem.Microservices.Seat.Infrastructure.Persistence;

/// <summary>
/// Entity Framework Core implementation of <see cref="IAircraftTypeRepository"/>.
/// </summary>
public sealed class EfAircraftTypeRepository : IAircraftTypeRepository
{
    private readonly SeatDbContext _context;
    private readonly ILogger<EfAircraftTypeRepository> _logger;

    public EfAircraftTypeRepository(
        SeatDbContext context,
        ILogger<EfAircraftTypeRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public Task<AircraftType?> GetByCodeAsync(string aircraftTypeCode, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<IReadOnlyList<AircraftType>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task CreateAsync(AircraftType aircraftType, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task UpdateAsync(AircraftType aircraftType, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}

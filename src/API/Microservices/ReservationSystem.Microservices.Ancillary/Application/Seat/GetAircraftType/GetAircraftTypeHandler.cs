using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Ancillary.Domain.Entities.Seat;
using ReservationSystem.Microservices.Ancillary.Domain.Repositories.Seat;

namespace ReservationSystem.Microservices.Ancillary.Application.Seat.GetAircraftType;

/// <summary>
/// Handles the <see cref="GetAircraftTypeQuery"/>.
/// Retrieves a single aircraft type by its code.
/// </summary>
public sealed class GetAircraftTypeHandler
{
    private readonly IAircraftTypeRepository _repository;
    private readonly ILogger<GetAircraftTypeHandler> _logger;

    public GetAircraftTypeHandler(
        IAircraftTypeRepository repository,
        ILogger<GetAircraftTypeHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<AircraftType?> HandleAsync(GetAircraftTypeQuery query, CancellationToken cancellationToken = default)
    {
        return await _repository.GetByCodeAsync(query.AircraftTypeCode, cancellationToken);
    }
}

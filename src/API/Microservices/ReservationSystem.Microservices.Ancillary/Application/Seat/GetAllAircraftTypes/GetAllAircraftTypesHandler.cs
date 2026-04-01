using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Ancillary.Domain.Entities.Seat;
using ReservationSystem.Microservices.Ancillary.Domain.Repositories.Seat;

namespace ReservationSystem.Microservices.Ancillary.Application.Seat.GetAllAircraftTypes;

/// <summary>
/// Handles the <see cref="GetAllAircraftTypesQuery"/>.
/// Retrieves all registered aircraft types.
/// </summary>
public sealed class GetAllAircraftTypesHandler
{
    private readonly IAircraftTypeRepository _repository;
    private readonly ILogger<GetAllAircraftTypesHandler> _logger;

    public GetAllAircraftTypesHandler(
        IAircraftTypeRepository repository,
        ILogger<GetAllAircraftTypesHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<IReadOnlyList<AircraftType>> HandleAsync(GetAllAircraftTypesQuery query, CancellationToken cancellationToken = default)
    {
        return await _repository.GetAllAsync(cancellationToken);
    }
}

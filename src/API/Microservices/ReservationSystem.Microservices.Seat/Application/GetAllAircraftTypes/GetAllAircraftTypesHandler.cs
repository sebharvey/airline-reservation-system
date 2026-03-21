using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Seat.Domain.Entities;
using ReservationSystem.Microservices.Seat.Domain.Repositories;

namespace ReservationSystem.Microservices.Seat.Application.GetAllAircraftTypes;

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

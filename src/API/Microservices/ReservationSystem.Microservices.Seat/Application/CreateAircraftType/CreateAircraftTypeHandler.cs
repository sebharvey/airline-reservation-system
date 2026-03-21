using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Seat.Domain.Entities;
using ReservationSystem.Microservices.Seat.Domain.Repositories;

namespace ReservationSystem.Microservices.Seat.Application.CreateAircraftType;

/// <summary>
/// Handles the <see cref="CreateAircraftTypeCommand"/>.
/// Creates and persists a new aircraft type.
/// </summary>
public sealed class CreateAircraftTypeHandler
{
    private readonly IAircraftTypeRepository _repository;
    private readonly ILogger<CreateAircraftTypeHandler> _logger;

    public CreateAircraftTypeHandler(
        IAircraftTypeRepository repository,
        ILogger<CreateAircraftTypeHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public Task<AircraftType> HandleAsync(CreateAircraftTypeCommand command, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}

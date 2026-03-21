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

    public async Task<AircraftType> HandleAsync(CreateAircraftTypeCommand command, CancellationToken cancellationToken = default)
    {
        var entity = AircraftType.Create(command.AircraftTypeCode, command.Manufacturer, command.TotalSeats, command.FriendlyName);
        var created = await _repository.CreateAsync(entity, cancellationToken);
        _logger.LogInformation("Created AircraftType {AircraftTypeCode}", created.AircraftTypeCode);
        return created;
    }
}

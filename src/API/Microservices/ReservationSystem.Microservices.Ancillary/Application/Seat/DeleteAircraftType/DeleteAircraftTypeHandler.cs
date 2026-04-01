using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Ancillary.Domain.Repositories.Seat;

namespace ReservationSystem.Microservices.Ancillary.Application.Seat.DeleteAircraftType;

/// <summary>
/// Handles the <see cref="DeleteAircraftTypeCommand"/>.
/// Deletes an aircraft type by its code.
/// </summary>
public sealed class DeleteAircraftTypeHandler
{
    private readonly IAircraftTypeRepository _repository;
    private readonly ILogger<DeleteAircraftTypeHandler> _logger;

    public DeleteAircraftTypeHandler(
        IAircraftTypeRepository repository,
        ILogger<DeleteAircraftTypeHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<bool> HandleAsync(DeleteAircraftTypeCommand command, CancellationToken cancellationToken = default)
    {
        var deleted = await _repository.DeleteAsync(command.AircraftTypeCode, cancellationToken);
        if (deleted)
            _logger.LogInformation("Deleted AircraftType {AircraftTypeCode}", command.AircraftTypeCode);
        return deleted;
    }
}

using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Seat.Domain.Entities;
using ReservationSystem.Microservices.Seat.Domain.Repositories;

namespace ReservationSystem.Microservices.Seat.Application.UpdateAircraftType;

/// <summary>
/// Handles the <see cref="UpdateAircraftTypeCommand"/>.
/// Updates an existing aircraft type.
/// </summary>
public sealed class UpdateAircraftTypeHandler
{
    private readonly IAircraftTypeRepository _repository;
    private readonly ILogger<UpdateAircraftTypeHandler> _logger;

    public UpdateAircraftTypeHandler(
        IAircraftTypeRepository repository,
        ILogger<UpdateAircraftTypeHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<AircraftType?> HandleAsync(UpdateAircraftTypeCommand command, CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetByCodeAsync(command.AircraftTypeCode, cancellationToken);
        if (existing is null) return null;

        var updated = AircraftType.Reconstitute(
            command.AircraftTypeCode,
            command.Manufacturer ?? existing.Manufacturer,
            command.FriendlyName ?? existing.FriendlyName,
            command.TotalSeats ?? existing.TotalSeats,
            command.IsActive ?? existing.IsActive,
            existing.CreatedAt,
            DateTime.UtcNow);

        var result = await _repository.UpdateAsync(updated, cancellationToken);
        _logger.LogInformation("Updated AircraftType {AircraftTypeCode}", command.AircraftTypeCode);
        return result;
    }
}

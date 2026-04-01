using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Ancillary.Domain.Entities.Seat;
using ReservationSystem.Microservices.Ancillary.Domain.Repositories.Seat;

namespace ReservationSystem.Microservices.Ancillary.Application.Seat.UpdateSeatmap;

/// <summary>
/// Handles the <see cref="UpdateSeatmapCommand"/>.
/// Updates an existing seatmap.
/// </summary>
public sealed class UpdateSeatmapHandler
{
    private readonly ISeatmapRepository _repository;
    private readonly ILogger<UpdateSeatmapHandler> _logger;

    public UpdateSeatmapHandler(
        ISeatmapRepository repository,
        ILogger<UpdateSeatmapHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Seatmap?> HandleAsync(UpdateSeatmapCommand command, CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetByIdAsync(command.SeatmapId, cancellationToken);
        if (existing is null) return null;

        var updated = Seatmap.Reconstitute(
            existing.SeatmapId,
            existing.AircraftTypeCode,
            existing.Version,
            command.IsActive ?? existing.IsActive,
            command.CabinLayout ?? existing.CabinLayout,
            existing.CreatedAt,
            DateTime.UtcNow);

        var result = await _repository.UpdateAsync(updated, cancellationToken);
        _logger.LogInformation("Updated Seatmap {SeatmapId}", command.SeatmapId);
        return result;
    }
}

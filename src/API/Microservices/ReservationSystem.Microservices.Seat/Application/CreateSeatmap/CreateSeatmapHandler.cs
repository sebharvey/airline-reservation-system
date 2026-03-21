using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Seat.Domain.Entities;
using ReservationSystem.Microservices.Seat.Domain.Repositories;

namespace ReservationSystem.Microservices.Seat.Application.CreateSeatmap;

/// <summary>
/// Handles the <see cref="CreateSeatmapCommand"/>.
/// Creates and persists a new seatmap.
/// </summary>
public sealed class CreateSeatmapHandler
{
    private readonly ISeatmapRepository _repository;
    private readonly ILogger<CreateSeatmapHandler> _logger;

    public CreateSeatmapHandler(
        ISeatmapRepository repository,
        ILogger<CreateSeatmapHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Seatmap> HandleAsync(CreateSeatmapCommand command, CancellationToken cancellationToken = default)
    {
        var entity = Seatmap.Create(command.AircraftTypeCode, 1, command.CabinLayout);
        var created = await _repository.CreateAsync(entity, cancellationToken);
        _logger.LogInformation("Created Seatmap {SeatmapId} for aircraft type {AircraftTypeCode}", created.SeatmapId, command.AircraftTypeCode);
        return created;
    }
}

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

    public Task<Seatmap> HandleAsync(CreateSeatmapCommand command, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}

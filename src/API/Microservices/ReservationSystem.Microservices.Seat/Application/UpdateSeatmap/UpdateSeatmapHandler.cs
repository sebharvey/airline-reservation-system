using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Seat.Domain.Entities;
using ReservationSystem.Microservices.Seat.Domain.Repositories;

namespace ReservationSystem.Microservices.Seat.Application.UpdateSeatmap;

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

    public Task<Seatmap?> HandleAsync(UpdateSeatmapCommand command, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}

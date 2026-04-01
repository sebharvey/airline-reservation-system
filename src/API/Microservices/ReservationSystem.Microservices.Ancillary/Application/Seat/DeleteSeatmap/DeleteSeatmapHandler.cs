using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Ancillary.Domain.Repositories.Seat;

namespace ReservationSystem.Microservices.Ancillary.Application.Seat.DeleteSeatmap;

/// <summary>
/// Handles the <see cref="DeleteSeatmapCommand"/>.
/// Deletes a seatmap definition by its identifier.
/// </summary>
public sealed class DeleteSeatmapHandler
{
    private readonly ISeatmapRepository _repository;
    private readonly ILogger<DeleteSeatmapHandler> _logger;

    public DeleteSeatmapHandler(
        ISeatmapRepository repository,
        ILogger<DeleteSeatmapHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public Task<bool> HandleAsync(DeleteSeatmapCommand command, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}

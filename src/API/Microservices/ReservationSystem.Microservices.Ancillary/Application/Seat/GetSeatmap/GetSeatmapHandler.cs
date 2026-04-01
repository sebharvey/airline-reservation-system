using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Ancillary.Domain.Entities.Seat;
using ReservationSystem.Microservices.Ancillary.Domain.Repositories.Seat;

namespace ReservationSystem.Microservices.Ancillary.Application.Seat.GetSeatmap;

/// <summary>
/// Handles the <see cref="GetSeatmapQuery"/>.
/// Retrieves the active seatmap for a given aircraft type.
/// </summary>
public sealed class GetSeatmapHandler
{
    private readonly ISeatmapRepository _repository;
    private readonly ILogger<GetSeatmapHandler> _logger;

    public GetSeatmapHandler(
        ISeatmapRepository repository,
        ILogger<GetSeatmapHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Seatmap?> HandleAsync(GetSeatmapQuery query, CancellationToken cancellationToken = default)
    {
        return await _repository.GetActiveByAircraftTypeCodeAsync(query.AircraftTypeCode, cancellationToken);
    }
}

using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Ancillary.Domain.Entities.Seat;
using ReservationSystem.Microservices.Ancillary.Domain.Repositories.Seat;

namespace ReservationSystem.Microservices.Ancillary.Application.Seat.GetAllSeatmaps;

/// <summary>
/// Handles the <see cref="GetAllSeatmapsQuery"/>.
/// Retrieves all seatmap definitions.
/// </summary>
public sealed class GetAllSeatmapsHandler
{
    private readonly ISeatmapRepository _repository;
    private readonly ILogger<GetAllSeatmapsHandler> _logger;

    public GetAllSeatmapsHandler(
        ISeatmapRepository repository,
        ILogger<GetAllSeatmapsHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public Task<IReadOnlyList<Seatmap>> HandleAsync(GetAllSeatmapsQuery query, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}

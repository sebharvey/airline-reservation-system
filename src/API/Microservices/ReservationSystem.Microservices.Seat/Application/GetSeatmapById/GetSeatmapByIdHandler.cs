using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Seat.Domain.Entities;
using ReservationSystem.Microservices.Seat.Domain.Repositories;

namespace ReservationSystem.Microservices.Seat.Application.GetSeatmapById;

/// <summary>
/// Handles the <see cref="GetSeatmapByIdQuery"/>.
/// Retrieves a seatmap definition by its identifier.
/// </summary>
public sealed class GetSeatmapByIdHandler
{
    private readonly ISeatmapRepository _repository;
    private readonly ILogger<GetSeatmapByIdHandler> _logger;

    public GetSeatmapByIdHandler(
        ISeatmapRepository repository,
        ILogger<GetSeatmapByIdHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public Task<Seatmap?> HandleAsync(GetSeatmapByIdQuery query, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}

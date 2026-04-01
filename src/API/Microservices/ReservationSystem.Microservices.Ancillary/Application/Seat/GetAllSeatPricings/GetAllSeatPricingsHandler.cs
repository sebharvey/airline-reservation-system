using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Ancillary.Domain.Entities.Seat;
using ReservationSystem.Microservices.Ancillary.Domain.Repositories.Seat;

namespace ReservationSystem.Microservices.Ancillary.Application.Seat.GetAllSeatPricings;

/// <summary>
/// Handles the <see cref="GetAllSeatPricingsQuery"/>.
/// Retrieves all seat pricing rules.
/// </summary>
public sealed class GetAllSeatPricingsHandler
{
    private readonly ISeatPricingRepository _repository;
    private readonly ILogger<GetAllSeatPricingsHandler> _logger;

    public GetAllSeatPricingsHandler(
        ISeatPricingRepository repository,
        ILogger<GetAllSeatPricingsHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<IReadOnlyList<SeatPricing>> HandleAsync(GetAllSeatPricingsQuery query, CancellationToken cancellationToken = default)
    {
        return await _repository.GetAllAsync(cancellationToken);
    }
}

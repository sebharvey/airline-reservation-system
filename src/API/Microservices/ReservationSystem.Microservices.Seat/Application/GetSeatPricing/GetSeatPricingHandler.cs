using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Seat.Domain.Entities;
using ReservationSystem.Microservices.Seat.Domain.Repositories;

namespace ReservationSystem.Microservices.Seat.Application.GetSeatPricing;

/// <summary>
/// Handles the <see cref="GetSeatPricingQuery"/>.
/// Retrieves a single seat pricing rule by its identifier.
/// </summary>
public sealed class GetSeatPricingHandler
{
    private readonly ISeatPricingRepository _repository;
    private readonly ILogger<GetSeatPricingHandler> _logger;

    public GetSeatPricingHandler(
        ISeatPricingRepository repository,
        ILogger<GetSeatPricingHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<SeatPricing?> HandleAsync(GetSeatPricingQuery query, CancellationToken cancellationToken = default)
    {
        return await _repository.GetByIdAsync(query.SeatPricingId, cancellationToken);
    }
}

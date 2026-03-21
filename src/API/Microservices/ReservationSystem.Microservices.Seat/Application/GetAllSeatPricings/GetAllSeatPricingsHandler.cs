using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Seat.Domain.Entities;
using ReservationSystem.Microservices.Seat.Domain.Repositories;

namespace ReservationSystem.Microservices.Seat.Application.GetAllSeatPricings;

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

    public Task<IReadOnlyList<SeatPricing>> HandleAsync(GetAllSeatPricingsQuery query, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}

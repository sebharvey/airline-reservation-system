using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Seat.Domain.Entities;
using ReservationSystem.Microservices.Seat.Domain.Repositories;

namespace ReservationSystem.Microservices.Seat.Application.UpdateSeatPricing;

/// <summary>
/// Handles the <see cref="UpdateSeatPricingCommand"/>.
/// Updates an existing seat pricing rule.
/// </summary>
public sealed class UpdateSeatPricingHandler
{
    private readonly ISeatPricingRepository _repository;
    private readonly ILogger<UpdateSeatPricingHandler> _logger;

    public UpdateSeatPricingHandler(
        ISeatPricingRepository repository,
        ILogger<UpdateSeatPricingHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public Task<SeatPricing?> HandleAsync(UpdateSeatPricingCommand command, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}

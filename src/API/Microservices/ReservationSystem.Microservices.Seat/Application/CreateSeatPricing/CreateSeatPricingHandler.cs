using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Seat.Domain.Entities;
using ReservationSystem.Microservices.Seat.Domain.Repositories;

namespace ReservationSystem.Microservices.Seat.Application.CreateSeatPricing;

/// <summary>
/// Handles the <see cref="CreateSeatPricingCommand"/>.
/// Creates and persists a new seat pricing rule.
/// </summary>
public sealed class CreateSeatPricingHandler
{
    private readonly ISeatPricingRepository _repository;
    private readonly ILogger<CreateSeatPricingHandler> _logger;

    public CreateSeatPricingHandler(
        ISeatPricingRepository repository,
        ILogger<CreateSeatPricingHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public Task<SeatPricing> HandleAsync(CreateSeatPricingCommand command, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}

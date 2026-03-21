using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Seat.Domain.Repositories;

namespace ReservationSystem.Microservices.Seat.Application.DeleteSeatPricing;

/// <summary>
/// Handles the <see cref="DeleteSeatPricingCommand"/>.
/// Deletes a seat pricing rule by its identifier.
/// </summary>
public sealed class DeleteSeatPricingHandler
{
    private readonly ISeatPricingRepository _repository;
    private readonly ILogger<DeleteSeatPricingHandler> _logger;

    public DeleteSeatPricingHandler(
        ISeatPricingRepository repository,
        ILogger<DeleteSeatPricingHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public Task<bool> HandleAsync(DeleteSeatPricingCommand command, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}

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

    public async Task<bool> HandleAsync(DeleteSeatPricingCommand command, CancellationToken cancellationToken = default)
    {
        var deleted = await _repository.DeleteAsync(command.SeatPricingId, cancellationToken);
        if (deleted)
            _logger.LogInformation("Deleted SeatPricing {SeatPricingId}", command.SeatPricingId);
        else
            _logger.LogWarning("Delete requested for unknown SeatPricing {SeatPricingId}", command.SeatPricingId);
        return deleted;
    }
}

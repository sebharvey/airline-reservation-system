using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Ancillary.Domain.Repositories.Seat;

namespace ReservationSystem.Microservices.Ancillary.Application.Seat.DeleteSeatPricing;

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

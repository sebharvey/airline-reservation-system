using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Ancillary.Domain.Entities.Seat;
using ReservationSystem.Microservices.Ancillary.Domain.Repositories.Seat;

namespace ReservationSystem.Microservices.Ancillary.Application.Seat.UpdateSeatPricing;

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

    public async Task<SeatPricing?> HandleAsync(UpdateSeatPricingCommand command, CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetByIdAsync(command.SeatPricingId, cancellationToken);
        if (existing is null) return null;

        var updated = SeatPricing.Reconstitute(
            command.SeatPricingId,
            command.CabinCode ?? existing.CabinCode,
            command.Description ?? existing.Description,
            command.Sequence ?? existing.Sequence,
            command.CurrencyCode ?? existing.CurrencyCode,
            command.Price ?? existing.Price,
            command.IsActive ?? existing.IsActive,
            command.ValidFrom ?? existing.ValidFrom,
            command.ValidTo ?? existing.ValidTo,
            existing.CreatedAt,
            DateTime.UtcNow);

        var result = await _repository.UpdateAsync(updated, cancellationToken);
        _logger.LogInformation("Updated SeatPricing {SeatPricingId}", command.SeatPricingId);
        return result;
    }
}

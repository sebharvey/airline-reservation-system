using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Ancillary.Domain.Entities.Bag;
using ReservationSystem.Microservices.Ancillary.Domain.Repositories.Bag;

namespace ReservationSystem.Microservices.Ancillary.Application.Bag.UpdateBagPricing;

public sealed class UpdateBagPricingHandler
{
    private readonly IBagPricingRepository _repository;
    private readonly ILogger<UpdateBagPricingHandler> _logger;

    public UpdateBagPricingHandler(IBagPricingRepository repository, ILogger<UpdateBagPricingHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<BagPricing?> HandleAsync(UpdateBagPricingCommand command, CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetByIdAsync(command.PricingId, cancellationToken);
        if (existing is null) return null;

        var updated = BagPricing.Reconstitute(
            command.PricingId, command.BagSequence, command.CurrencyCode, command.Price,
            command.IsActive, command.ValidFrom, command.ValidTo,
            existing.CreatedAt, DateTime.UtcNow);

        var result = await _repository.UpdateAsync(updated, cancellationToken);
        _logger.LogInformation("Updated BagPricing {PricingId}", command.PricingId);
        return result;
    }
}

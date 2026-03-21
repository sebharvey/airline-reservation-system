using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Bags.Domain.Repositories;

namespace ReservationSystem.Microservices.Bags.Application.DeleteBagPricing;

public sealed class DeleteBagPricingHandler
{
    private readonly IBagPricingRepository _repository;
    private readonly ILogger<DeleteBagPricingHandler> _logger;

    public DeleteBagPricingHandler(IBagPricingRepository repository, ILogger<DeleteBagPricingHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<bool> HandleAsync(DeleteBagPricingCommand command, CancellationToken cancellationToken = default)
    {
        var deleted = await _repository.DeleteAsync(command.PricingId, cancellationToken);
        if (deleted)
            _logger.LogInformation("Deleted BagPricing {PricingId}", command.PricingId);
        else
            _logger.LogWarning("Delete requested for unknown BagPricing {PricingId}", command.PricingId);
        return deleted;
    }
}

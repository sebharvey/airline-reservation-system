using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Bags.Domain.Repositories;

namespace ReservationSystem.Microservices.Bags.Application.UpdateBagPricing;

public sealed class UpdateBagPricingHandler
{
    private readonly IBagPricingRepository _repository;
    private readonly ILogger<UpdateBagPricingHandler> _logger;

    public UpdateBagPricingHandler(IBagPricingRepository repository, ILogger<UpdateBagPricingHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Domain.Entities.BagPricing?> HandleAsync(
        UpdateBagPricingCommand command,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}

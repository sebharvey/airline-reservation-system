using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Bags.Domain.Repositories;

namespace ReservationSystem.Microservices.Bags.Application.GetBagPricing;

public sealed class GetBagPricingHandler
{
    private readonly IBagPricingRepository _repository;
    private readonly ILogger<GetBagPricingHandler> _logger;

    public GetBagPricingHandler(IBagPricingRepository repository, ILogger<GetBagPricingHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Domain.Entities.BagPricing?> HandleAsync(
        GetBagPricingQuery query,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}

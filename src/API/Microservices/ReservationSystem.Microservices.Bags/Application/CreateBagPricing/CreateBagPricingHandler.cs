using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Bags.Domain.Repositories;

namespace ReservationSystem.Microservices.Bags.Application.CreateBagPricing;

public sealed class CreateBagPricingHandler
{
    private readonly IBagPricingRepository _repository;
    private readonly ILogger<CreateBagPricingHandler> _logger;

    public CreateBagPricingHandler(IBagPricingRepository repository, ILogger<CreateBagPricingHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Domain.Entities.BagPricing> HandleAsync(
        CreateBagPricingCommand command,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}

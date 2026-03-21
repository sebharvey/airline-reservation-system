using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Bags.Domain.Repositories;

namespace ReservationSystem.Microservices.Bags.Application.GetAllBagPricings;

public sealed class GetAllBagPricingsHandler
{
    private readonly IBagPricingRepository _repository;
    private readonly ILogger<GetAllBagPricingsHandler> _logger;

    public GetAllBagPricingsHandler(IBagPricingRepository repository, ILogger<GetAllBagPricingsHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Domain.Entities.BagPricing>> HandleAsync(
        GetAllBagPricingsQuery query,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}

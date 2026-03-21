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

    public async Task HandleAsync(
        DeleteBagPricingCommand command,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}

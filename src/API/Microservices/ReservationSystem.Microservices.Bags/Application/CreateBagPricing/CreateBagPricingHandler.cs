using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Bags.Domain.Entities;
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

    public async Task<BagPricing> HandleAsync(CreateBagPricingCommand command, CancellationToken cancellationToken = default)
    {
        var pricing = BagPricing.Create(command.BagSequence, command.CurrencyCode, command.Price,
            command.ValidFrom, command.ValidTo);
        var created = await _repository.CreateAsync(pricing, cancellationToken);
        _logger.LogInformation("Created BagPricing {PricingId} for sequence {BagSequence}", created.PricingId, command.BagSequence);
        return created;
    }
}

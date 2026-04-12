using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Ancillary.Domain.Entities.Product;
using ReservationSystem.Microservices.Ancillary.Domain.Repositories.Product;

namespace ReservationSystem.Microservices.Ancillary.Application.Product.CreateProductPrice;

public sealed class CreateProductPriceHandler
{
    private readonly IProductPriceRepository _repository;
    private readonly ILogger<CreateProductPriceHandler> _logger;

    public CreateProductPriceHandler(IProductPriceRepository repository, ILogger<CreateProductPriceHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<ProductPrice> HandleAsync(CreateProductPriceCommand command, CancellationToken cancellationToken = default)
    {
        var price = ProductPrice.Create(command.ProductId, command.CurrencyCode, command.Price, command.Tax);
        var created = await _repository.CreateAsync(price, cancellationToken);
        _logger.LogInformation("Created ProductPrice {PriceId} for product {ProductId} currency {CurrencyCode}",
            created.PriceId, created.ProductId, created.CurrencyCode);
        return created;
    }
}

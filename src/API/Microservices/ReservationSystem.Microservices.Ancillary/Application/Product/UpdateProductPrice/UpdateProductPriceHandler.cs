using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Ancillary.Domain.Entities.Product;
using ReservationSystem.Microservices.Ancillary.Domain.Repositories.Product;

namespace ReservationSystem.Microservices.Ancillary.Application.Product.UpdateProductPrice;

public sealed class UpdateProductPriceHandler
{
    private readonly IProductPriceRepository _repository;
    private readonly ILogger<UpdateProductPriceHandler> _logger;

    public UpdateProductPriceHandler(IProductPriceRepository repository, ILogger<UpdateProductPriceHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<ProductPrice?> HandleAsync(UpdateProductPriceCommand command, CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetByIdAsync(command.PriceId, cancellationToken);
        if (existing is null) return null;

        var updated = ProductPrice.Reconstitute(
            existing.PriceId, existing.ProductId, existing.OfferId, existing.CurrencyCode,
            command.Price, command.Tax, command.IsActive,
            existing.CreatedAt, DateTime.UtcNow);

        return await _repository.UpdateAsync(updated, cancellationToken);
    }
}

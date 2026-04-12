using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Ancillary.Domain.Repositories.Product;

namespace ReservationSystem.Microservices.Ancillary.Application.Product.DeleteProductPrice;

public sealed class DeleteProductPriceHandler
{
    private readonly IProductPriceRepository _repository;
    private readonly ILogger<DeleteProductPriceHandler> _logger;

    public DeleteProductPriceHandler(IProductPriceRepository repository, ILogger<DeleteProductPriceHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<bool> HandleAsync(DeleteProductPriceCommand command, CancellationToken cancellationToken = default)
    {
        var deleted = await _repository.DeleteAsync(command.PriceId, cancellationToken);
        if (deleted)
            _logger.LogInformation("Deleted ProductPrice {PriceId}", command.PriceId);
        return deleted;
    }
}

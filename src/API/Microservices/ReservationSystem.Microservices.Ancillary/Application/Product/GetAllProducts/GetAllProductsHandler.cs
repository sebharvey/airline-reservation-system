using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Ancillary.Domain.Repositories.Product;
using ProductEntity = ReservationSystem.Microservices.Ancillary.Domain.Entities.Product.Product;

namespace ReservationSystem.Microservices.Ancillary.Application.Product.GetAllProducts;

public sealed class GetAllProductsHandler
{
    private readonly IProductRepository _repository;
    private readonly ILogger<GetAllProductsHandler> _logger;

    public GetAllProductsHandler(IProductRepository repository, ILogger<GetAllProductsHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ProductEntity>> HandleAsync(GetAllProductsQuery query, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving all products");
        return await _repository.GetAllAsync(cancellationToken);
    }
}

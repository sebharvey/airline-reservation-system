using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Ancillary.Domain.Repositories.Product;

namespace ReservationSystem.Microservices.Ancillary.Application.Product.GetProduct;

public sealed class GetProductHandler
{
    private readonly IProductRepository _repository;
    private readonly ILogger<GetProductHandler> _logger;

    public GetProductHandler(IProductRepository repository, ILogger<GetProductHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Entities.Product.Product?> HandleAsync(GetProductQuery query, CancellationToken cancellationToken = default)
    {
        return await _repository.GetByIdAsync(query.ProductId, cancellationToken);
    }
}

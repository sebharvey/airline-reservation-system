using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Ancillary.Domain.Entities.Product;
using ReservationSystem.Microservices.Ancillary.Domain.Repositories.Product;

namespace ReservationSystem.Microservices.Ancillary.Application.Product.GetProductGroup;

public sealed class GetProductGroupHandler
{
    private readonly IProductGroupRepository _repository;
    private readonly ILogger<GetProductGroupHandler> _logger;

    public GetProductGroupHandler(IProductGroupRepository repository, ILogger<GetProductGroupHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<ProductGroup?> HandleAsync(GetProductGroupQuery query, CancellationToken cancellationToken = default)
    {
        return await _repository.GetByIdAsync(query.ProductGroupId, cancellationToken);
    }
}

using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Ancillary.Domain.Entities.Product;
using ReservationSystem.Microservices.Ancillary.Domain.Repositories.Product;

namespace ReservationSystem.Microservices.Ancillary.Application.Product.GetAllProductGroups;

public sealed class GetAllProductGroupsHandler
{
    private readonly IProductGroupRepository _repository;
    private readonly ILogger<GetAllProductGroupsHandler> _logger;

    public GetAllProductGroupsHandler(IProductGroupRepository repository, ILogger<GetAllProductGroupsHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ProductGroup>> HandleAsync(GetAllProductGroupsQuery query, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving all product groups");
        return await _repository.GetAllAsync(cancellationToken);
    }
}

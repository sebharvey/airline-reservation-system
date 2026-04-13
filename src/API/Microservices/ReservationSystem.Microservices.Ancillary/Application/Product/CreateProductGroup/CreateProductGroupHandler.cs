using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Ancillary.Domain.Entities.Product;
using ReservationSystem.Microservices.Ancillary.Domain.Repositories.Product;

namespace ReservationSystem.Microservices.Ancillary.Application.Product.CreateProductGroup;

public sealed class CreateProductGroupHandler
{
    private readonly IProductGroupRepository _repository;
    private readonly ILogger<CreateProductGroupHandler> _logger;

    public CreateProductGroupHandler(IProductGroupRepository repository, ILogger<CreateProductGroupHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<ProductGroup> HandleAsync(CreateProductGroupCommand command, CancellationToken cancellationToken = default)
    {
        var group = ProductGroup.Create(command.Name, command.SortOrder);
        var created = await _repository.CreateAsync(group, cancellationToken);
        _logger.LogInformation("Created ProductGroup {ProductGroupId} '{Name}'", created.ProductGroupId, created.Name);
        return created;
    }
}

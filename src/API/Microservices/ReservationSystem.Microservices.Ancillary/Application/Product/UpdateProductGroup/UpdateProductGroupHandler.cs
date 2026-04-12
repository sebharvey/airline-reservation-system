using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Ancillary.Domain.Entities.Product;
using ReservationSystem.Microservices.Ancillary.Domain.Repositories.Product;

namespace ReservationSystem.Microservices.Ancillary.Application.Product.UpdateProductGroup;

public sealed class UpdateProductGroupHandler
{
    private readonly IProductGroupRepository _repository;
    private readonly ILogger<UpdateProductGroupHandler> _logger;

    public UpdateProductGroupHandler(IProductGroupRepository repository, ILogger<UpdateProductGroupHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<ProductGroup?> HandleAsync(UpdateProductGroupCommand command, CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetByIdAsync(command.ProductGroupId, cancellationToken);
        if (existing is null) return null;

        var updated = ProductGroup.Reconstitute(
            existing.ProductGroupId, command.Name, command.IsActive,
            existing.CreatedAt, DateTime.UtcNow);

        return await _repository.UpdateAsync(updated, cancellationToken);
    }
}

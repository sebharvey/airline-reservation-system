using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Ancillary.Domain.Repositories.Product;

namespace ReservationSystem.Microservices.Ancillary.Application.Product.DeleteProductGroup;

public sealed class DeleteProductGroupHandler
{
    private readonly IProductGroupRepository _repository;
    private readonly IProductRepository _productRepository;
    private readonly ILogger<DeleteProductGroupHandler> _logger;

    public DeleteProductGroupHandler(IProductGroupRepository repository, IProductRepository productRepository, ILogger<DeleteProductGroupHandler> logger)
    {
        _repository = repository;
        _productRepository = productRepository;
        _logger = logger;
    }

    public async Task<DeleteProductGroupResult> HandleAsync(DeleteProductGroupCommand command, CancellationToken cancellationToken = default)
    {
        var products = await _productRepository.GetByGroupAsync(command.ProductGroupId, cancellationToken);
        if (products.Count > 0)
            return DeleteProductGroupResult.HasProducts;

        var deleted = await _repository.DeleteAsync(command.ProductGroupId, cancellationToken);
        if (!deleted)
            return DeleteProductGroupResult.NotFound;

        _logger.LogInformation("Deleted ProductGroup {ProductGroupId}", command.ProductGroupId);
        return DeleteProductGroupResult.Deleted;
    }
}

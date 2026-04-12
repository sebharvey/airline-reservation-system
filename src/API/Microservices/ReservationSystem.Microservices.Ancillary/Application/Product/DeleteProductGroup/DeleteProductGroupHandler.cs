using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Ancillary.Domain.Repositories.Product;

namespace ReservationSystem.Microservices.Ancillary.Application.Product.DeleteProductGroup;

public sealed class DeleteProductGroupHandler
{
    private readonly IProductGroupRepository _repository;
    private readonly ILogger<DeleteProductGroupHandler> _logger;

    public DeleteProductGroupHandler(IProductGroupRepository repository, ILogger<DeleteProductGroupHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<bool> HandleAsync(DeleteProductGroupCommand command, CancellationToken cancellationToken = default)
    {
        var deleted = await _repository.DeleteAsync(command.ProductGroupId, cancellationToken);
        if (deleted)
            _logger.LogInformation("Deleted ProductGroup {ProductGroupId}", command.ProductGroupId);
        return deleted;
    }
}

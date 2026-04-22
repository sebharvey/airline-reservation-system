using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Ancillary.Domain.Repositories.Product;
using ProductEntity = ReservationSystem.Microservices.Ancillary.Domain.Entities.Product.Product;

namespace ReservationSystem.Microservices.Ancillary.Application.Product.UpdateProduct;

public sealed class UpdateProductHandler
{
    private readonly IProductRepository _repository;
    private readonly ILogger<UpdateProductHandler> _logger;

    public UpdateProductHandler(IProductRepository repository, ILogger<UpdateProductHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<ProductEntity?> HandleAsync(UpdateProductCommand command, CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetByIdAsync(command.ProductId, cancellationToken);
        if (existing is null) return null;

        var updated = ProductEntity.Reconstitute(
            existing.ProductId, command.ProductGroupId, command.Name, command.Description,
            command.IsSegmentSpecific,
            string.IsNullOrWhiteSpace(command.SsrCode) ? null : command.SsrCode.ToUpperInvariant(),
            command.ImageBase64, command.AvailableChannels, command.IsActive,
            existing.CreatedAt, DateTime.UtcNow);

        return await _repository.UpdateAsync(updated, cancellationToken);
    }
}

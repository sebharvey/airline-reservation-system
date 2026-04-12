using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Ancillary.Domain.Entities.Product;
using ReservationSystem.Microservices.Ancillary.Domain.Repositories.Product;

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

    public async Task<Entities.Product.Product?> HandleAsync(UpdateProductCommand command, CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetByIdAsync(command.ProductId, cancellationToken);
        if (existing is null) return null;

        var updated = Entities.Product.Product.Reconstitute(
            existing.ProductId, command.ProductGroupId, command.Name, command.Description,
            command.IsSegmentSpecific,
            string.IsNullOrWhiteSpace(command.SsrCode) ? null : command.SsrCode.ToUpperInvariant(),
            command.ImageBase64, command.IsActive,
            existing.CreatedAt, DateTime.UtcNow);

        return await _repository.UpdateAsync(updated, cancellationToken);
    }
}

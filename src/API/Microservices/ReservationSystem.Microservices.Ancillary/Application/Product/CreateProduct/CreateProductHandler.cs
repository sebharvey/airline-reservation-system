using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Ancillary.Domain.Repositories.Product;
using ProductEntity = ReservationSystem.Microservices.Ancillary.Domain.Entities.Product.Product;

namespace ReservationSystem.Microservices.Ancillary.Application.Product.CreateProduct;

public sealed class CreateProductHandler
{
    private readonly IProductRepository _repository;
    private readonly ILogger<CreateProductHandler> _logger;

    public CreateProductHandler(IProductRepository repository, ILogger<CreateProductHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<ProductEntity> HandleAsync(CreateProductCommand command, CancellationToken cancellationToken = default)
    {
        var product = ProductEntity.Create(
            command.ProductGroupId, command.Name, command.Description,
            command.IsSegmentSpecific, command.SsrCode, command.ImageBase64,
            command.AvailableChannels);

        var created = await _repository.CreateAsync(product, cancellationToken);
        _logger.LogInformation("Created Product {ProductId} '{Name}'", created.ProductId, created.Name);
        return created;
    }
}

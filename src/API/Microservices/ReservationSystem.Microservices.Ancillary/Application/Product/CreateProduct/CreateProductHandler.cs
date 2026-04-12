using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Ancillary.Domain.Repositories.Product;

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

    public async Task<Entities.Product.Product> HandleAsync(CreateProductCommand command, CancellationToken cancellationToken = default)
    {
        var product = Entities.Product.Product.Create(
            command.ProductGroupId, command.Name, command.Description,
            command.IsSegmentSpecific, command.SsrCode, command.ImageBase64);

        var created = await _repository.CreateAsync(product, cancellationToken);
        _logger.LogInformation("Created Product {ProductId} '{Name}'", created.ProductId, created.Name);
        return created;
    }
}

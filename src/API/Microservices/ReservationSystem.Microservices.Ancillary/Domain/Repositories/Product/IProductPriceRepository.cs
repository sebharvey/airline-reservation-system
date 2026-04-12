using ReservationSystem.Microservices.Ancillary.Domain.Entities.Product;

namespace ReservationSystem.Microservices.Ancillary.Domain.Repositories.Product;

public interface IProductPriceRepository
{
    Task<ProductPrice?> GetByIdAsync(Guid priceId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProductPrice>> GetByProductAsync(Guid productId, CancellationToken cancellationToken = default);
    Task<ProductPrice> CreateAsync(ProductPrice price, CancellationToken cancellationToken = default);
    Task<ProductPrice?> UpdateAsync(ProductPrice price, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid priceId, CancellationToken cancellationToken = default);
}

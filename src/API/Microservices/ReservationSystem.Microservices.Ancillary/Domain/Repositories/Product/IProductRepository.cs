using ProductEntity = ReservationSystem.Microservices.Ancillary.Domain.Entities.Product.Product;

namespace ReservationSystem.Microservices.Ancillary.Domain.Repositories.Product;

public interface IProductRepository
{
    Task<ProductEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProductEntity>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProductEntity>> GetByGroupAsync(Guid productGroupId, CancellationToken cancellationToken = default);
    Task<ProductEntity> CreateAsync(ProductEntity product, CancellationToken cancellationToken = default);
    Task<ProductEntity?> UpdateAsync(ProductEntity product, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

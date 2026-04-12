using ReservationSystem.Microservices.Ancillary.Domain.Entities.Product;

namespace ReservationSystem.Microservices.Ancillary.Domain.Repositories.Product;

public interface IProductGroupRepository
{
    Task<ProductGroup?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProductGroup>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<ProductGroup> CreateAsync(ProductGroup group, CancellationToken cancellationToken = default);
    Task<ProductGroup?> UpdateAsync(ProductGroup group, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

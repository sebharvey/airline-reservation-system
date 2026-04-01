using ReservationSystem.Microservices.Ancillary.Domain.Entities.Bag;

namespace ReservationSystem.Microservices.Ancillary.Domain.Repositories.Bag;

public interface IBagPricingRepository
{
    Task<BagPricing?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<BagPricing?> GetBySequenceAsync(int bagSequence, string currencyCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BagPricing>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BagPricing>> GetAllActiveAsync(CancellationToken cancellationToken = default);
    Task<BagPricing> CreateAsync(BagPricing pricing, CancellationToken cancellationToken = default);
    Task<BagPricing?> UpdateAsync(BagPricing pricing, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

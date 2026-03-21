namespace ReservationSystem.Microservices.Bags.Domain.Repositories;

public interface IBagPricingRepository
{
    Task<Entities.BagPricing?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Entities.BagPricing?> GetBySequenceAsync(int bagSequence, string currencyCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Entities.BagPricing>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Entities.BagPricing>> GetAllActiveAsync(CancellationToken cancellationToken = default);
    Task<Entities.BagPricing> CreateAsync(Entities.BagPricing pricing, CancellationToken cancellationToken = default);
    Task<Entities.BagPricing?> UpdateAsync(Entities.BagPricing pricing, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

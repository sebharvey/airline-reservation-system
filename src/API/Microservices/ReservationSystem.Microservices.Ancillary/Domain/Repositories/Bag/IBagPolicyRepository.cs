using ReservationSystem.Microservices.Ancillary.Domain.Entities.Bag;

namespace ReservationSystem.Microservices.Ancillary.Domain.Repositories.Bag;

public interface IBagPolicyRepository
{
    Task<BagPolicy?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<BagPolicy?> GetByCabinCodeAsync(string cabinCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BagPolicy>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<BagPolicy> CreateAsync(BagPolicy policy, CancellationToken cancellationToken = default);
    Task<BagPolicy?> UpdateAsync(BagPolicy policy, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

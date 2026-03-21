namespace ReservationSystem.Microservices.Bags.Domain.Repositories;

public interface IBagPolicyRepository
{
    Task<Entities.BagPolicy?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Entities.BagPolicy?> GetByCabinCodeAsync(string cabinCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Entities.BagPolicy>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Entities.BagPolicy> CreateAsync(Entities.BagPolicy policy, CancellationToken cancellationToken = default);
    Task<Entities.BagPolicy?> UpdateAsync(Entities.BagPolicy policy, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

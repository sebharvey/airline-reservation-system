using ReservationSystem.Template.TemplateApi.Domain.Entities;

namespace ReservationSystem.Template.TemplateApi.Domain.Repositories;

/// <summary>
/// Port (interface) for Person persistence.
/// Defined in Domain so the Application layer can depend on it without
/// taking a dependency on Infrastructure. The EF Core implementation lives in
/// Infrastructure/Persistence and is registered via DI at startup.
/// </summary>
public interface IPersonRepository
{
    Task<Person?> GetByIdAsync(int personId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Person>> GetAllAsync(CancellationToken cancellationToken = default);

    Task CreateAsync(Person person, CancellationToken cancellationToken = default);

    Task UpdateAsync(Person person, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(int personId, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(int personId, CancellationToken cancellationToken = default);
}

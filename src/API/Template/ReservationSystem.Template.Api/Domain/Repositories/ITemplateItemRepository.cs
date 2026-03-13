using ReservationSystem.Template.Api.Domain.Entities;

namespace ReservationSystem.Template.Api.Domain.Repositories;

/// <summary>
/// Port (interface) for TemplateItem persistence.
/// Defined in Domain so the Application layer can depend on it without
/// taking a dependency on Infrastructure. The SQL implementation lives in
/// Infrastructure/Persistence and is registered via DI at startup.
/// </summary>
public interface ITemplateItemRepository
{
    Task<TemplateItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TemplateItem>> GetAllAsync(CancellationToken cancellationToken = default);

    Task CreateAsync(TemplateItem item, CancellationToken cancellationToken = default);

    Task UpdateAsync(TemplateItem item, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

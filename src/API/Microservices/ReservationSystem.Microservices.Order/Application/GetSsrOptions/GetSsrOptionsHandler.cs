using ReservationSystem.Microservices.Order.Domain.Entities;
using ReservationSystem.Microservices.Order.Domain.Repositories;

namespace ReservationSystem.Microservices.Order.Application.GetSsrOptions;

public sealed class GetSsrOptionsHandler
{
    private readonly ISsrCatalogueRepository _repository;

    public GetSsrOptionsHandler(ISsrCatalogueRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<SsrCatalogueEntry>> HandleAsync(
        GetSsrOptionsQuery query,
        CancellationToken cancellationToken = default)
    {
        return await _repository.GetActiveAsync(cancellationToken);
    }
}

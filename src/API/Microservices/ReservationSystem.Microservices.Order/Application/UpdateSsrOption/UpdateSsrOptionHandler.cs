using ReservationSystem.Microservices.Order.Domain.Entities;
using ReservationSystem.Microservices.Order.Domain.Repositories;

namespace ReservationSystem.Microservices.Order.Application.UpdateSsrOption;

public sealed class UpdateSsrOptionHandler
{
    private readonly ISsrCatalogueRepository _repository;

    public UpdateSsrOptionHandler(ISsrCatalogueRepository repository)
    {
        _repository = repository;
    }

    public async Task<SsrCatalogueEntry?> HandleAsync(UpdateSsrOptionCommand command, CancellationToken cancellationToken = default)
    {
        return await _repository.UpdateAsync(command.SsrCode, command.Label, command.Category, cancellationToken);
    }
}

using ReservationSystem.Microservices.Order.Domain.Repositories;

namespace ReservationSystem.Microservices.Order.Application.DeactivateSsrOption;

public sealed class DeactivateSsrOptionHandler
{
    private readonly ISsrCatalogueRepository _repository;

    public DeactivateSsrOptionHandler(ISsrCatalogueRepository repository)
    {
        _repository = repository;
    }

    public async Task<bool> HandleAsync(DeactivateSsrOptionCommand command, CancellationToken cancellationToken = default)
    {
        return await _repository.DeactivateAsync(command.SsrCode, cancellationToken);
    }
}

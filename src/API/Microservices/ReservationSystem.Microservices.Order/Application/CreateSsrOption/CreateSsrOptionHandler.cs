using ReservationSystem.Microservices.Order.Domain.Entities;
using ReservationSystem.Microservices.Order.Domain.Repositories;

namespace ReservationSystem.Microservices.Order.Application.CreateSsrOption;

public sealed class CreateSsrOptionHandler
{
    private readonly ISsrCatalogueRepository _repository;

    public CreateSsrOptionHandler(ISsrCatalogueRepository repository)
    {
        _repository = repository;
    }

    public async Task<SsrCatalogueEntry?> HandleAsync(CreateSsrOptionCommand command, CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetByCodeAsync(command.SsrCode, cancellationToken);
        if (existing is not null) return null;

        var entry = new SsrCatalogueEntry
        {
            SsrCatalogueId = Guid.NewGuid(),
            SsrCode = command.SsrCode.ToUpperInvariant(),
            Label = command.Label,
            Category = command.Category,
            IsActive = true
        };

        return await _repository.CreateAsync(entry, cancellationToken);
    }
}

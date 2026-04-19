using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Offer.Domain.Repositories;

namespace ReservationSystem.Microservices.Offer.Application.DeleteFareFamily;

public sealed class DeleteFareFamilyHandler
{
    private readonly IOfferRepository _repository;
    private readonly ILogger<DeleteFareFamilyHandler> _logger;

    public DeleteFareFamilyHandler(IOfferRepository repository, ILogger<DeleteFareFamilyHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<bool> HandleAsync(DeleteFareFamilyCommand command, CancellationToken ct = default)
    {
        var deleted = await _repository.DeleteFareFamilyAsync(command.FareFamilyId, ct);

        if (deleted)
            _logger.LogInformation("Deleted FareFamily {FareFamilyId}", command.FareFamilyId);

        return deleted;
    }
}

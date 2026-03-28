using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Offer.Domain.Repositories;

namespace ReservationSystem.Microservices.Offer.Application.DeleteFareRule;

public sealed class DeleteFareRuleHandler
{
    private readonly IOfferRepository _repository;
    private readonly ILogger<DeleteFareRuleHandler> _logger;

    public DeleteFareRuleHandler(IOfferRepository repository, ILogger<DeleteFareRuleHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<bool> HandleAsync(DeleteFareRuleCommand command, CancellationToken ct = default)
    {
        var deleted = await _repository.DeleteFareRuleAsync(command.FareRuleId, ct);

        if (deleted)
            _logger.LogInformation("Deleted FareRule {FareRuleId}", command.FareRuleId);
        else
            _logger.LogWarning("FareRule {FareRuleId} not found for deletion", command.FareRuleId);

        return deleted;
    }
}

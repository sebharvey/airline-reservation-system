using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Offer.Domain.Entities;
using ReservationSystem.Microservices.Offer.Domain.Repositories;

namespace ReservationSystem.Microservices.Offer.Application.UpdateFareRule;

public sealed class UpdateFareRuleHandler
{
    private readonly IOfferRepository _repository;
    private readonly ILogger<UpdateFareRuleHandler> _logger;

    public UpdateFareRuleHandler(IOfferRepository repository, ILogger<UpdateFareRuleHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<FareRule> HandleAsync(UpdateFareRuleCommand command, CancellationToken ct = default)
    {
        var fareRule = await _repository.GetFareRuleByIdAsync(command.FareRuleId, ct)
            ?? throw new KeyNotFoundException($"FareRule {command.FareRuleId} not found.");

        fareRule.Update(
            command.RuleType, command.FlightNumber, command.FareBasisCode, command.FareFamily,
            command.CabinCode, command.BookingClass, command.CurrencyCode,
            command.MinAmount, command.MaxAmount,
            command.MinPoints, command.MaxPoints, command.PointsTaxes,
            command.TaxLines,
            command.IsRefundable, command.IsChangeable,
            command.ChangeFeeAmount, command.CancellationFeeAmount,
            string.IsNullOrEmpty(command.ValidFrom) ? null : DateTimeOffset.Parse(command.ValidFrom),
            string.IsNullOrEmpty(command.ValidTo) ? null : DateTimeOffset.Parse(command.ValidTo));

        await _repository.UpdateFareRuleAsync(fareRule, ct);

        _logger.LogInformation("Updated FareRule {FareRuleId} ({FareBasisCode}, {RuleType})",
            fareRule.FareRuleId, command.FareBasisCode, command.RuleType);

        return fareRule;
    }
}

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
            command.FlightNumber, command.FareBasisCode, command.FareFamily,
            command.CabinCode, command.BookingClass, command.CurrencyCode,
            command.BaseFareAmount, command.TaxAmount,
            command.IsRefundable, command.IsChangeable,
            command.ChangeFeeAmount, command.CancellationFeeAmount,
            command.PointsPrice, command.PointsTaxes,
            string.IsNullOrEmpty(command.ValidFrom) ? null : DateTime.Parse(command.ValidFrom),
            string.IsNullOrEmpty(command.ValidTo) ? null : DateTime.Parse(command.ValidTo));

        await _repository.UpdateFareRuleAsync(fareRule, ct);

        _logger.LogInformation("Updated FareRule {FareRuleId} ({FareBasisCode})",
            fareRule.FareRuleId, command.FareBasisCode);

        return fareRule;
    }
}

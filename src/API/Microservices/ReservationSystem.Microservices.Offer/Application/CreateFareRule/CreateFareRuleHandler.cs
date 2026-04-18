using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Offer.Domain.Entities;
using ReservationSystem.Microservices.Offer.Domain.Repositories;

namespace ReservationSystem.Microservices.Offer.Application.CreateFareRule;

public sealed class CreateFareRuleHandler
{
    private readonly IOfferRepository _repository;
    private readonly ILogger<CreateFareRuleHandler> _logger;

    public CreateFareRuleHandler(IOfferRepository repository, ILogger<CreateFareRuleHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<FareRule> HandleAsync(CreateFareRuleCommand command, CancellationToken ct = default)
    {
        var fareRule = FareRule.Create(
            command.RuleType, command.FlightNumber, command.FareBasisCode, command.FareFamily,
            command.CabinCode, command.BookingClass, command.CurrencyCode,
            command.MinAmount, command.MaxAmount,
            command.MinPoints, command.MaxPoints, command.PointsTaxes,
            command.TaxLines,
            command.IsRefundable, command.IsChangeable,
            command.ChangeFeeAmount, command.CancellationFeeAmount,
            command.IsPrivate,
            string.IsNullOrEmpty(command.ValidFrom) ? null : DateTimeOffset.Parse(command.ValidFrom),
            string.IsNullOrEmpty(command.ValidTo) ? null : DateTimeOffset.Parse(command.ValidTo));

        await _repository.CreateFareRuleAsync(fareRule, ct);

        _logger.LogInformation("Created FareRule {FareRuleId} ({FareBasisCode}, {RuleType})",
            fareRule.FareRuleId, command.FareBasisCode, command.RuleType);

        return fareRule;
    }
}

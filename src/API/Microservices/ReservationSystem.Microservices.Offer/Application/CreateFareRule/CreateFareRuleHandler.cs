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
            command.FlightNumber, command.FareBasisCode, command.FareFamily,
            command.CabinCode, command.BookingClass, command.CurrencyCode,
            command.BaseFareAmount, command.TaxAmount,
            command.IsRefundable, command.IsChangeable,
            command.ChangeFeeAmount, command.CancellationFeeAmount,
            command.PointsPrice, command.PointsTaxes,
            DateTime.Parse(command.ValidFrom), DateTime.Parse(command.ValidTo));

        await _repository.CreateFareRuleAsync(fareRule, ct);

        _logger.LogInformation("Created FareRule {FareRuleId} ({FareBasisCode})",
            fareRule.FareRuleId, command.FareBasisCode);

        return fareRule;
    }
}

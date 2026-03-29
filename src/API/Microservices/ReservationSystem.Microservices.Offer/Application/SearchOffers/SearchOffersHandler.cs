using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Offer.Domain.Entities;
using ReservationSystem.Microservices.Offer.Domain.Repositories;

namespace ReservationSystem.Microservices.Offer.Application.SearchOffers;

public sealed class SearchOffersHandler
{
    private readonly IOfferRepository _repository;
    private readonly ILogger<SearchOffersHandler> _logger;

    public SearchOffersHandler(IOfferRepository repository, ILogger<SearchOffersHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<IReadOnlyList<StoredOffer>> HandleAsync(SearchOffersCommand command, CancellationToken ct = default)
    {
        var departureDate = DateOnly.Parse(command.DepartureDate);
        var bookingType = string.IsNullOrEmpty(command.BookingType) ? "Revenue" : command.BookingType;

        // 1. Find all active flights on the route where total seats available >= pax count.
        //    Sold-out flights (SeatsAvailable < paxCount) are excluded by the query.
        var inventories = await _repository.SearchAvailableInventoryAsync(
            command.Origin, command.Destination, departureDate, command.PaxCount, ct);

        var offers = new List<StoredOffer>();

        foreach (var inventory in inventories)
        {
            // 2. Iterate every cabin on the flight; skip cabins that cannot accommodate the party.
            foreach (var cabin in inventory.Cabins)
            {
                if (cabin.SeatsAvailable < command.PaxCount)
                    continue;

                // Optional cabin filter — if the caller specified a cabin code, skip others.
                if (command.CabinCode is not null && cabin.CabinCode != command.CabinCode)
                    continue;

                // 3. Retrieve all applicable fare rules for this cabin in tier order
                //    (global default → flight default → flight + date window).
                var rules = await _repository.GetApplicableFareRulesAsync(
                    inventory.FlightNumber, cabin.CabinCode, departureDate, ct);

                if (rules.Count == 0)
                    continue;

                // 4. Apply cascade: rules arrive ordered least-to-most-specific.
                //    Iterating in order and overwriting by (FareBasisCode, RuleType) means the last
                //    (most-specific) rule for each combination wins — O(n) single pass.
                var effective = new Dictionary<string, FareRule>(StringComparer.Ordinal);
                foreach (var rule in rules)
                    effective[$"{rule.FareBasisCode}:{rule.RuleType}"] = rule;

                // 5. For each effective rule, derive a fare and create a stored offer snapshot.
                foreach (var rule in effective.Values)
                {
                    // Revenue search: skip pure-points rules that carry no base fare.
                    if (bookingType != "Reward" && rule.RuleType == "Points" && rule.MinAmount is null)
                        continue;

                    // Reward search: skip rules that carry no points price.
                    if (bookingType == "Reward" && rule.MinPoints is null)
                        continue;

                    var fare = BuildFareFromRule(inventory.InventoryId, rule);
                    await _repository.CreateFareAsync(fare, ct);

                    var storedOffer = StoredOffer.Create(inventory, fare, bookingType);
                    await _repository.CreateStoredOfferAsync(storedOffer, ct);
                    offers.Add(storedOffer);
                }
            }
        }

        _logger.LogInformation(
            "Search {Origin}-{Destination} on {Date} cabin {Cabin}: {Count} offers created",
            command.Origin, command.Destination, command.DepartureDate,
            command.CabinCode ?? "ALL", offers.Count);

        return offers.AsReadOnly();
    }

    /// <summary>
    /// Builds a <see cref="Fare"/> snapshot from a resolved <see cref="FareRule"/>.
    /// MinAmount is used as the base fare; ValidFrom/ValidTo default to a wide open window
    /// when the rule carries no date bounds.
    /// </summary>
    private static Fare BuildFareFromRule(Guid inventoryId, FareRule rule)
    {
        var validFrom = rule.ValidFrom ?? new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var validTo   = rule.ValidTo   ?? new DateTime(2099, 12, 31, 23, 59, 59, DateTimeKind.Utc);

        return Fare.Create(
            inventoryId:          inventoryId,
            fareBasisCode:        rule.FareBasisCode,
            fareFamily:           rule.FareFamily,
            cabinCode:            rule.CabinCode,
            bookingClass:         rule.BookingClass,
            currencyCode:         rule.CurrencyCode ?? "GBP",
            baseFareAmount:       rule.MinAmount ?? 0m,
            taxAmount:            rule.TaxAmount ?? 0m,
            isRefundable:         rule.IsRefundable,
            isChangeable:         rule.IsChangeable,
            changeFeeAmount:      rule.ChangeFeeAmount,
            cancellationFeeAmount: rule.CancellationFeeAmount,
            pointsPrice:          rule.MinPoints,
            pointsTaxes:          rule.PointsTaxes,
            validFrom:            validFrom,
            validTo:              validTo);
    }
}

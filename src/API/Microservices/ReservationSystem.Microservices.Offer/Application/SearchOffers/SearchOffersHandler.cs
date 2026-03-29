using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Offer.Domain.Entities;
using ReservationSystem.Microservices.Offer.Domain.Repositories;

namespace ReservationSystem.Microservices.Offer.Application.SearchOffers;

/// <summary>
/// Pairs a stored offer with the in-memory inventory used to create it, so the
/// calling function endpoint can build the flight-level response directly from
/// the inventory without an additional DB round-trip.
/// </summary>
public sealed record SearchOfferResult(StoredOffer Offer, FlightInventory Inventory);

public sealed class SearchOffersHandler
{
    private readonly IOfferRepository _repository;
    private readonly ILogger<SearchOffersHandler> _logger;

    public SearchOffersHandler(IOfferRepository repository, ILogger<SearchOffersHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<IReadOnlyList<SearchOfferResult>> HandleAsync(SearchOffersCommand command, CancellationToken ct = default)
    {
        var departureDate = DateOnly.Parse(command.DepartureDate);
        var bookingType = string.IsNullOrEmpty(command.BookingType) ? "Revenue" : command.BookingType;

        // 1. Find all active flights on the route where total seats available >= pax count.
        var inventories = await _repository.SearchAvailableInventoryAsync(
            command.Origin, command.Destination, departureDate, command.PaxCount, ct);

        // One SessionId shared across all flights returned for this search.
        var sessionId = Guid.NewGuid();
        var results = new List<SearchOfferResult>();

        foreach (var inventory in inventories)
        {
            // 2. Collect eligible fares across all cabins for this flight.
            var flightFares = new List<(Fare Fare, Guid FareRuleId)>();

            foreach (var cabin in inventory.Cabins)
            {
                if (cabin.SeatsAvailable < command.PaxCount)
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

                // 5. For each effective rule, derive a fare snapshot.
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
                    flightFares.Add((fare, rule.FareRuleId));
                }
            }

            if (flightFares.Count == 0)
                continue;

            // 6. Create ONE stored offer per flight. FaresInfo holds fare items only;
            //    flight details remain authoritative in FlightInventory.
            var storedOffer = StoredOffer.Create(inventory, flightFares, bookingType, sessionId);
            await _repository.CreateStoredOfferAsync(storedOffer, ct);
            results.Add(new SearchOfferResult(storedOffer, inventory));
        }

        _logger.LogInformation(
            "Search {Origin}-{Destination} on {Date} session {SessionId}: {Count} flight offers created",
            command.Origin, command.Destination, command.DepartureDate, sessionId, results.Count);

        return results.AsReadOnly();
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

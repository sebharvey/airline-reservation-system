using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Offer.Domain.Entities;
using ReservationSystem.Microservices.Offer.Domain.Repositories;

namespace ReservationSystem.Microservices.Offer.Application.SearchOffers;

/// <summary>
/// The single stored offer for a search, paired with all matching inventories so
/// the calling function endpoint can build the flight-level response without
/// additional DB round-trips.
/// </summary>
public sealed record SearchOfferResult(StoredOffer Offer, IReadOnlyList<FlightInventory> Inventories);

public sealed class SearchOffersHandler
{
    private readonly IOfferRepository _repository;
    private readonly ILogger<SearchOffersHandler> _logger;

    public SearchOffersHandler(IOfferRepository repository, ILogger<SearchOffersHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<SearchOfferResult?> HandleAsync(SearchOffersCommand command, CancellationToken ct = default)
    {
        var departureDate = DateOnly.Parse(command.DepartureDate);
        var bookingType = string.IsNullOrEmpty(command.BookingType) ? "Revenue" : command.BookingType;

        // 1. Find all active flights on the route where total seats available >= pax count.
        var inventories = await _repository.SearchAvailableInventoryAsync(
            command.Origin, command.Destination, departureDate, command.PaxCount, ct);

        if (inventories.Count == 0)
        {
            _logger.LogInformation(
                "Search {Origin}-{Destination} on {Date}: no inventory available",
                command.Origin, command.Destination, command.DepartureDate);
            return null;
        }

        // 2. Collect the unique flight numbers and cabin codes across all results so we can
        //    fetch every applicable fare rule in a single query rather than one per cabin.
        var flightNumbers = inventories.Select(i => i.FlightNumber).Distinct().ToList();
        var cabinCodes    = inventories
            .SelectMany(i => i.Cabins)
            .Where(c => c.SeatsAvailable >= command.PaxCount)
            .Select(c => c.CabinCode)
            .Distinct()
            .ToList();

        if (cabinCodes.Count == 0)
        {
            _logger.LogInformation(
                "Search {Origin}-{Destination} on {Date}: no cabin has enough seats for {PaxCount} passengers",
                command.Origin, command.Destination, command.DepartureDate, command.PaxCount);
            return null;
        }

        // 3. Single DB round-trip for all fare rules needed by this search.
        var allRules = await _repository.GetApplicableFareRulesForFlightsAsync(
            flightNumbers, cabinCodes, departureDate, ct);

        var sessionId         = Guid.NewGuid();
        var allInventoryFares = new List<(FlightInventory Inventory, IReadOnlyList<(Fare Fare, Guid FareRuleId)> Fares)>();
        var faresToInsert     = new List<Fare>();

        foreach (var inventory in inventories)
        {
            // 4. Collect eligible fares across all cabins for this flight.
            var flightFares = new List<(Fare Fare, Guid FareRuleId)>();

            foreach (var cabin in inventory.Cabins)
            {
                if (cabin.SeatsAvailable < command.PaxCount)
                    continue;

                // 5. Filter the pre-fetched rule set to this cabin and flight (including global
                //    defaults where FlightNumber is null), preserving the least-to-most-specific
                //    ordering the repository returns them in.
                var rules = allRules
                    .Where(r => r.CabinCode == cabin.CabinCode
                             && (r.FlightNumber is null || r.FlightNumber == inventory.FlightNumber))
                    .ToList();

                if (rules.Count == 0)
                    continue;

                // 6. Apply cascade: overwrite by (FareBasisCode, RuleType) so the last
                //    (most-specific) rule wins — O(n) single pass.
                var effective = new Dictionary<string, FareRule>(StringComparer.Ordinal);
                foreach (var rule in rules)
                    effective[$"{rule.FareBasisCode}:{rule.RuleType}"] = rule;

                // 7. For each effective rule, derive a fare snapshot.
                foreach (var rule in effective.Values)
                {
                    // Revenue search: skip pure-points rules that carry no base fare.
                    if (bookingType != "Reward" && rule.RuleType == "Points" && rule.MinAmount is null)
                        continue;

                    // Reward search: skip rules that carry no points price.
                    if (bookingType == "Reward" && rule.MinPoints is null)
                        continue;

                    var fare = BuildFareFromRule(inventory.InventoryId, rule, cabin);
                    faresToInsert.Add(fare);
                    flightFares.Add((fare, rule.FareRuleId));
                }
            }

            if (flightFares.Count == 0)
                continue;

            allInventoryFares.Add((inventory, flightFares));
        }

        if (allInventoryFares.Count == 0)
        {
            _logger.LogInformation(
                "Search {Origin}-{Destination} on {Date}: no offers available",
                command.Origin, command.Destination, command.DepartureDate);
            return null;
        }

        // 8. Persist all fares in a single batch, then create the stored offer.
        await _repository.BatchCreateFaresAsync(faresToInsert, ct);

        var storedOffer = StoredOffer.Create(allInventoryFares, bookingType, sessionId);
        await _repository.CreateStoredOfferAsync(storedOffer, ct);

        _logger.LogInformation(
            "Search {Origin}-{Destination} on {Date} session {SessionId}: {Count} flight offers stored in single record",
            command.Origin, command.Destination, command.DepartureDate, sessionId, allInventoryFares.Count);

        return new SearchOfferResult(storedOffer, allInventoryFares.Select(ivf => ivf.Inventory).ToList());
    }

    /// <summary>
    /// Builds a <see cref="Fare"/> snapshot from a resolved <see cref="FareRule"/>.
    /// The base fare is dynamically priced between MinAmount and MaxAmount (and MinPoints to
    /// MaxPoints for award fares) based on how full the cabin is: an empty cabin yields the
    /// minimum price and a sold-out cabin yields the maximum price.  When MaxAmount/MaxPoints
    /// is not set the minimum price is used unchanged.
    /// ValidFrom/ValidTo default to a wide open window when the rule carries no date bounds.
    /// </summary>
    private static Fare BuildFareFromRule(Guid inventoryId, FareRule rule, CabinInventory cabin)
    {
        var validFrom = rule.ValidFrom ?? new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var validTo   = rule.ValidTo   ?? new DateTimeOffset(2099, 12, 31, 23, 59, 59, TimeSpan.Zero);

        // Occupancy ratio: proportion of total seats that are sold or held (0 = empty, 1 = full).
        var occupancyRatio = cabin.TotalSeats > 0
            ? Math.Clamp((double)(cabin.SeatsSold + cabin.SeatsHeld) / cabin.TotalSeats, 0.0, 1.0)
            : 0.0;

        var baseFareAmount = ComputeDynamicPrice(rule.MinAmount, rule.MaxAmount, occupancyRatio);
        var pointsPrice    = ComputeDynamicPoints(rule.MinPoints, rule.MaxPoints, occupancyRatio);

        return Fare.Create(
            inventoryId:           inventoryId,
            fareBasisCode:         rule.FareBasisCode,
            fareFamily:            rule.FareFamily,
            cabinCode:             rule.CabinCode,
            bookingClass:          rule.BookingClass,
            currencyCode:          rule.CurrencyCode ?? "GBP",
            baseFareAmount:        baseFareAmount,
            taxAmount:             rule.TaxAmount ?? 0m,
            isRefundable:          rule.IsRefundable,
            isChangeable:          rule.IsChangeable,
            changeFeeAmount:       rule.ChangeFeeAmount,
            cancellationFeeAmount: rule.CancellationFeeAmount,
            pointsPrice:           pointsPrice,
            pointsTaxes:           rule.PointsTaxes,
            validFrom:             validFrom,
            validTo:               validTo);
    }

    /// <summary>
    /// Linearly interpolates between <paramref name="min"/> and <paramref name="max"/> at the
    /// given <paramref name="occupancyRatio"/>.  Returns <paramref name="min"/> when
    /// <paramref name="max"/> is absent, rounded to 2 decimal places.
    /// </summary>
    private static decimal ComputeDynamicPrice(decimal? min, decimal? max, double occupancyRatio)
    {
        var minVal = min ?? 0m;
        if (max is null || max <= minVal)
            return minVal;

        return Math.Round(minVal + (max.Value - minVal) * (decimal)occupancyRatio, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Same interpolation as <see cref="ComputeDynamicPrice"/> but for integer points values.
    /// </summary>
    private static int? ComputeDynamicPoints(int? min, int? max, double occupancyRatio)
    {
        if (min is null)
            return null;
        if (max is null || max <= min)
            return min;

        return (int)Math.Round(min.Value + (max.Value - min.Value) * occupancyRatio, MidpointRounding.AwayFromZero);
    }
}

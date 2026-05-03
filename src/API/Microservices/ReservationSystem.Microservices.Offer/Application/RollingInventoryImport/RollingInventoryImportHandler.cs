using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Offer.Application.BatchCreateFlights;
using ReservationSystem.Microservices.Offer.Application.CreateFlight;
using ReservationSystem.Microservices.Offer.Domain.Entities;
using ReservationSystem.Microservices.Offer.Domain.ExternalServices;
using ReservationSystem.Microservices.Offer.Domain.Repositories;

namespace ReservationSystem.Microservices.Offer.Application.RollingInventoryImport;

/// <summary>
/// Extends the rolling inventory window and backfills any missed days.
///
/// Flow:
///   1. Calculate the window: today through today + 1 month (inclusive).
///   2. Fetch all schedules from Schedule MS.
///   3. Fetch aircraft type configurations from Seat MS.
///   4. For every date in the window, for each schedule valid on that date and matching
///      the days-of-week bitmask, build a BatchFlightItem.
///   5. Batch-create inventory in the Offer repository (existing records are skipped
///      automatically, so days already populated incur no side-effects).
///   6. For each newly created inventory, resolve applicable fare rules from the
///      repository and create Fare entities in bulk.
///
/// Scanning the full window on every run ensures that any days missed due to prior
/// failures are backfilled without manual intervention.
/// </summary>
public sealed class RollingInventoryImportHandler
{
    private readonly IScheduleServiceClient _scheduleClient;
    private readonly ISeatServiceClient _seatClient;
    private readonly BatchCreateFlightsHandler _batchCreateFlightsHandler;
    private readonly IOfferRepository _repository;
    private readonly ILogger<RollingInventoryImportHandler> _logger;

    public RollingInventoryImportHandler(
        IScheduleServiceClient scheduleClient,
        ISeatServiceClient seatClient,
        BatchCreateFlightsHandler batchCreateFlightsHandler,
        IOfferRepository repository,
        ILogger<RollingInventoryImportHandler> logger)
    {
        _scheduleClient = scheduleClient;
        _seatClient = seatClient;
        _batchCreateFlightsHandler = batchCreateFlightsHandler;
        _repository = repository;
        _logger = logger;
    }

    public async Task HandleAsync(CancellationToken ct = default)
    {
        var today = DateTime.UtcNow.Date;
        var windowEnd = today.AddMonths(1);

        _logger.LogInformation(
            "RollingInventoryImport: scanning window {WindowStart:yyyy-MM-dd} to {WindowEnd:yyyy-MM-dd}",
            today, windowEnd);

        // 1. Fetch all schedules from Schedule MS.
        var schedulesResult = await _scheduleClient.GetSchedulesAsync(ct);

        if (schedulesResult.Count == 0)
        {
            _logger.LogInformation("RollingInventoryImport: no schedules found — nothing to import");
            return;
        }

        // 2. Fetch aircraft type configurations from Seat MS.
        var aircraftTypesResult = await _seatClient.GetAircraftTypesAsync(ct);

        var cabinsByAircraftType = aircraftTypesResult.AircraftTypes
            .Where(a => a.CabinCounts is { Count: > 0 })
            .ToDictionary(
                a => a.AircraftTypeCode,
                a => (IReadOnlyList<CabinCount>)a.CabinCounts!,
                StringComparer.OrdinalIgnoreCase);

        // 3. Build flight items for every date in the window.
        //    Existing records are skipped by BatchCreateInventoryAsync, so days already
        //    populated are harmless to re-submit — this is what provides the backfill.
        var flightItems = new List<BatchFlightItem>();

        for (var date = today; date <= windowEnd; date = date.AddDays(1))
        {
            var dayBit = date.DayOfWeek switch
            {
                DayOfWeek.Monday    => 1,
                DayOfWeek.Tuesday   => 2,
                DayOfWeek.Wednesday => 4,
                DayOfWeek.Thursday  => 8,
                DayOfWeek.Friday    => 16,
                DayOfWeek.Saturday  => 32,
                DayOfWeek.Sunday    => 64,
                _                   => 0
            };

            foreach (var schedule in schedulesResult.Schedules)
            {
                if (!cabinsByAircraftType.TryGetValue(schedule.AircraftType, out var cabins))
                    continue;

                var validFrom = DateTime.Parse(schedule.ValidFrom).Date;
                var validTo = DateTime.Parse(schedule.ValidTo).Date;

                if (date < validFrom || date > validTo)
                    continue;

                if ((schedule.DaysOfWeek & dayBit) == 0)
                    continue;

                var cabinItems = cabins
                    .Select(c => new CabinItem(c.Cabin, c.Count))
                    .ToList()
                    .AsReadOnly();

                flightItems.Add(new BatchFlightItem(
                    schedule.FlightNumber,
                    date.ToString("yyyy-MM-dd"),
                    schedule.DepartureTime,
                    schedule.ArrivalTime,
                    schedule.ArrivalDayOffset,
                    schedule.Origin,
                    schedule.Destination,
                    schedule.AircraftType,
                    cabinItems,
                    schedule.DepartureTimeUtc,
                    schedule.ArrivalTimeUtc,
                    schedule.ArrivalDayOffsetUtc));
            }
        }

        if (flightItems.Count == 0)
        {
            _logger.LogInformation("RollingInventoryImport: no flights operate in window — nothing to create");
            return;
        }

        // 4. Batch-create inventory; existing records are skipped automatically.
        var batchResult = await _batchCreateFlightsHandler.HandleAsync(
            new BatchCreateFlightsCommand(flightItems.AsReadOnly()), ct);

        _logger.LogInformation(
            "RollingInventoryImport: inventories created={Created}, skipped={Skipped} across window {WindowStart:yyyy-MM-dd} to {WindowEnd:yyyy-MM-dd}",
            batchResult.Created.Count, batchResult.SkippedCount, today, windowEnd);

        if (batchResult.Created.Count == 0)
            return;

        // 5. Apply fare rules to each newly created inventory, grouped by departure date
        //    so the correct date-windowed fare rules are resolved for each day.
        var faresToCreate = new List<Fare>();

        var createdByDate = batchResult.Created
            .GroupBy(i => i.DepartureDate)
            .ToList();

        foreach (var dateGroup in createdByDate)
        {
            var departureDateOnly = dateGroup.Key;
            var flightNumbers = dateGroup.Select(i => i.FlightNumber).Distinct().ToList();
            var cabinCodes = dateGroup
                .SelectMany(i => i.Cabins.Select(c => c.CabinCode))
                .Distinct()
                .ToList();

            var allApplicableRules = await _repository.GetApplicableFareRulesForFlightsAsync(
                flightNumbers, cabinCodes, departureDateOnly, ct: ct);

            var rulesByCabinAndFlight = allApplicableRules
                .GroupBy(r => (r.CabinCode, r.FlightNumber))
                .ToDictionary(g => g.Key, g => (IReadOnlyList<FareRule>)g.ToList().AsReadOnly());

            foreach (var inventory in dateGroup)
            {
                foreach (var cabin in inventory.Cabins)
                {
                    var specificRules = rulesByCabinAndFlight.GetValueOrDefault(
                        (cabin.CabinCode, inventory.FlightNumber), []);
                    var globalRules = rulesByCabinAndFlight.GetValueOrDefault(
                        (cabin.CabinCode, null), []);

                    foreach (var rule in specificRules.Concat(globalRules))
                    {
                        var validFrom = rule.ValidFrom ?? DateTimeOffset.UtcNow;
                        var validTo = rule.ValidTo ?? DateTimeOffset.UtcNow.AddYears(1);

                        faresToCreate.Add(Fare.Create(
                            inventory.InventoryId,
                            rule.FareBasisCode,
                            rule.FareFamily,
                            rule.CabinCode,
                            rule.BookingClass,
                            rule.CurrencyCode ?? "GBP",
                            rule.MinAmount ?? 0m,
                            rule.GetTotalTaxAmount(),
                            rule.IsRefundable,
                            rule.IsChangeable,
                            rule.ChangeFeeAmount,
                            rule.CancellationFeeAmount,
                            rule.RuleType == "Points" ? rule.MinPoints : null,
                            rule.RuleType == "Points" ? rule.PointsTaxes : null,
                            validFrom,
                            validTo));
                    }
                }
            }
        }

        if (faresToCreate.Count > 0)
        {
            await _repository.BatchCreateFaresAsync(faresToCreate.AsReadOnly(), ct);
            _logger.LogInformation(
                "RollingInventoryImport: created {FareCount} fares for {NewInventoryCount} new inventories",
                faresToCreate.Count, batchResult.Created.Count);
        }
    }
}

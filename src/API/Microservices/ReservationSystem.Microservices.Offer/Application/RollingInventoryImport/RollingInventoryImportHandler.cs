using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Offer.Application.BatchCreateFlights;
using ReservationSystem.Microservices.Offer.Application.CreateFlight;
using ReservationSystem.Microservices.Offer.Domain.Entities;
using ReservationSystem.Microservices.Offer.Domain.ExternalServices;
using ReservationSystem.Microservices.Offer.Domain.Repositories;

namespace ReservationSystem.Microservices.Offer.Application.RollingInventoryImport;

/// <summary>
/// Extends the rolling inventory window by importing the next day of flights at the
/// 3-month boundary. Runs daily via a timer trigger so the system always holds exactly
/// 3 months of forward inventory.
///
/// Flow:
///   1. Calculate targetDate = today + 1 month.
///   2. Fetch all schedules from Schedule MS (accepted anti-pattern for timer triggers).
///   3. Fetch aircraft type configurations from Seat MS.
///   4. For each schedule valid on targetDate and matching the days-of-week bitmask,
///      build a BatchFlightItem for that single date.
///   5. Batch-create inventory in the Offer repository (existing records are skipped).
///   6. For each newly created inventory, resolve applicable fare rules from the
///      repository and create Fare entities in bulk.
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
        var targetDate = DateTime.UtcNow.Date.AddMonths(3);
        var targetDateOnly = DateOnly.FromDateTime(targetDate);

        _logger.LogInformation(
            "RollingInventoryImport: importing inventory for target date {TargetDate:yyyy-MM-dd}",
            targetDate);

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

        // 3. Build flight items for the target date.
        var dayBit = targetDate.DayOfWeek switch
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

        var flightItems = new List<BatchFlightItem>();

        foreach (var schedule in schedulesResult.Schedules)
        {
            if (!cabinsByAircraftType.TryGetValue(schedule.AircraftType, out var cabins))
            {
                _logger.LogDebug(
                    "RollingInventoryImport: no cabin config for aircraft type '{AircraftType}' — skipping {FlightNumber}",
                    schedule.AircraftType, schedule.FlightNumber);
                continue;
            }

            var validFrom = DateTime.Parse(schedule.ValidFrom).Date;
            var validTo = DateTime.Parse(schedule.ValidTo).Date;

            // Skip schedules that don't cover the target date.
            if (targetDate < validFrom || targetDate > validTo)
                continue;

            // Skip if this flight doesn't operate on the target day of week.
            if ((schedule.DaysOfWeek & dayBit) == 0)
                continue;

            var cabinItems = cabins
                .Select(c => new CabinItem(c.Cabin, c.Count))
                .ToList()
                .AsReadOnly();

            flightItems.Add(new BatchFlightItem(
                schedule.FlightNumber,
                targetDate.ToString("yyyy-MM-dd"),
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

        if (flightItems.Count == 0)
        {
            _logger.LogInformation(
                "RollingInventoryImport: no flights operate on {TargetDate:yyyy-MM-dd} — nothing to create",
                targetDate);
            return;
        }

        // 4. Batch-create inventory; existing records are skipped automatically.
        var batchResult = await _batchCreateFlightsHandler.HandleAsync(
            new BatchCreateFlightsCommand(flightItems.AsReadOnly()), ct);

        _logger.LogInformation(
            "RollingInventoryImport: inventories created={Created}, skipped={Skipped} for {TargetDate:yyyy-MM-dd}",
            batchResult.Created.Count, batchResult.SkippedCount, targetDate);

        // 5. Apply fare rules to each newly created inventory.
        var flightNumbers = batchResult.Created.Select(i => i.FlightNumber).Distinct().ToList();
        var cabinCodes = batchResult.Created
            .SelectMany(i => i.Cabins.Select(c => c.CabinCode))
            .Distinct()
            .ToList();

        if (batchResult.Created.Count == 0)
            return;

        var allApplicableRules = await _repository.GetApplicableFareRulesForFlightsAsync(
            flightNumbers, cabinCodes, targetDateOnly, ct);

        var rulesByCabinAndFlight = allApplicableRules
            .GroupBy(r => (r.CabinCode, r.FlightNumber))
            .ToDictionary(g => g.Key, g => (IReadOnlyList<FareRule>)g.ToList().AsReadOnly());

        var faresToCreate = new List<Fare>();

        foreach (var inventory in batchResult.Created)
        {
            foreach (var cabin in inventory.Cabins)
            {
                // Rules scoped to this exact flight + cabin, plus global rules for this cabin.
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
                        rule.TaxAmount ?? 0m,
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

        if (faresToCreate.Count > 0)
        {
            await _repository.BatchCreateFaresAsync(faresToCreate.AsReadOnly(), ct);
            _logger.LogInformation(
                "RollingInventoryImport: created {FareCount} fares for {TargetDate:yyyy-MM-dd}",
                faresToCreate.Count, targetDate);
        }
    }
}

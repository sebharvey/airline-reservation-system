using Microsoft.Extensions.Logging;
using ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices.Dto;
using ReservationSystem.Orchestration.Operations.Models.Responses;

namespace ReservationSystem.Orchestration.Operations.Application.ImportSchedulesToInventory;

/// <summary>
/// Imports flight schedules from the Schedule MS into the Offer MS inventory tables.
///
/// Flow:
///   1. Fetch all schedules from Schedule MS GET /v1/schedules.
///   2. Fetch aircraft type configurations from Seat MS GET /v1/aircraft-types.
///   3. Build a lookup of aircraftTypeCode → cabin seat counts from the Seat MS response.
///   4. Fetch all fare rules from the Offer MS once, grouped by cabin code.
///   5. For each schedule, resolve its cabin config by AircraftType; skip if not found.
///   6. Enumerate operating dates from ValidFrom/ValidTo and DaysOfWeek bitmask.
///   7. For each operating date × cabin, build a batch inventory creation request.
///   8. Call Offer MS POST /v1/flights/batch — existing records are skipped automatically.
///   9. For each newly created inventory, apply matching fare rules from storage.
///  10. Return a summary of schedules processed, inventories created/skipped, and fares created.
/// </summary>
public sealed class ImportSchedulesToInventoryHandler
{
    private readonly ScheduleServiceClient _scheduleClient;
    private readonly SeatServiceClient _seatClient;
    private readonly OfferServiceClient _offerClient;
    private readonly FareRuleServiceClient _fareRuleClient;
    private readonly ILogger<ImportSchedulesToInventoryHandler> _logger;

    public ImportSchedulesToInventoryHandler(
        ScheduleServiceClient scheduleClient,
        SeatServiceClient seatClient,
        OfferServiceClient offerClient,
        FareRuleServiceClient fareRuleClient,
        ILogger<ImportSchedulesToInventoryHandler> logger)
    {
        _scheduleClient = scheduleClient;
        _seatClient = seatClient;
        _offerClient = offerClient;
        _fareRuleClient = fareRuleClient;
        _logger = logger;
    }

    public async Task<ImportSchedulesToInventoryResponse> HandleAsync(
        ImportSchedulesToInventoryCommand command,
        CancellationToken cancellationToken = default)
    {
        // 1. Retrieve persisted schedules from Schedule MS, optionally scoped by group.
        var schedulesResult = await _scheduleClient.GetSchedulesAsync(command.ScheduleGroupId, cancellationToken);

        _logger.LogInformation(
            "ImportSchedulesToInventory: found {Count} schedules to process",
            schedulesResult.Count);

        if (schedulesResult.Count == 0)
        {
            return new ImportSchedulesToInventoryResponse
            {
                SchedulesProcessed = 0,
                InventoriesCreated = 0,
                InventoriesSkipped = 0,
                FaresCreated = 0
            };
        }

        // 2. Fetch aircraft type configurations from Seat MS.
        var aircraftTypesResult = await _seatClient.GetAircraftTypesAsync(cancellationToken);

        // 3. Build a lookup of aircraftTypeCode → cabin seat counts from the Seat MS response.
        var cabinsByAircraftType = aircraftTypesResult.AircraftTypes
            .Where(a => a.CabinCounts is { Count: > 0 })
            .ToDictionary(
                a => a.AircraftTypeCode,
                a => (IReadOnlyList<CabinCountDto>)a.CabinCounts!,
                StringComparer.OrdinalIgnoreCase);

        // 4. Fetch all fare rules from the Offer MS once; group by cabin code.
        IReadOnlyList<FareRuleDto> fareRules = [];
        try
        {
            fareRules = await _fareRuleClient.SearchFareRulesAsync(cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ImportSchedulesToInventory: could not fetch fare rules — inventory will be created without fares");
        }

        var fareRulesByCabin = fareRules
            .GroupBy(r => r.CabinCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<FareRuleDto>)g.ToList().AsReadOnly(), StringComparer.OrdinalIgnoreCase);

        // 5. Build a batch payload: one entry per schedule × operating date (all cabins combined).
        //    Dates before today are skipped regardless of the schedule's ValidFrom.
        //    Schedules whose aircraft type has no registered config in the Seat MS are skipped.
        var flightItems = new List<object>();
        var schedulesWithConfig = new List<ScheduleItemDto>();
        var today = DateTime.UtcNow.Date;

        // Hard cap at 1 month from today. If the caller provides an earlier ToDate, honour it.
        var oneMonthCap = today.AddMonths(1);
        var importCeiling = (command.ToDate.HasValue && command.ToDate.Value.Date < oneMonthCap)
            ? command.ToDate.Value.Date
            : oneMonthCap;

        foreach (var schedule in schedulesResult.Schedules)
        {
            if (!cabinsByAircraftType.TryGetValue(schedule.AircraftType, out var cabins))
            {
                _logger.LogWarning(
                    "ImportSchedulesToInventory: no cabin config for aircraft type '{AircraftType}' in Seat MS — skipping schedule {FlightNumber}",
                    schedule.AircraftType, schedule.FlightNumber);
                continue;
            }

            schedulesWithConfig.Add(schedule);

            var validFrom = DateTime.Parse(schedule.ValidFrom);
            var validTo = DateTime.Parse(schedule.ValidTo);

            // Never import beyond the 1-month ceiling.
            if (validTo.Date > importCeiling)
                validTo = importCeiling;

            // Never import dates in the past — floor the start to today.
            var effectiveFrom = validFrom.Date < today ? today : validFrom.Date;

            var operatingDates = GetOperatingDates(effectiveFrom, validTo, schedule.DaysOfWeek);

            var cabinsPayload = cabins
                .Select(c => new { cabinCode = c.Cabin, totalSeats = c.Count })
                .ToArray();

            foreach (var date in operatingDates)
            {
                flightItems.Add(new
                {
                    flightNumber = schedule.FlightNumber,
                    departureDate = date.ToString("yyyy-MM-dd"),
                    departureTime = schedule.DepartureTime,
                    arrivalTime = schedule.ArrivalTime,
                    arrivalDayOffset = schedule.ArrivalDayOffset,
                    departureTimeUtc = schedule.DepartureTimeUtc,
                    arrivalTimeUtc = schedule.ArrivalTimeUtc,
                    arrivalDayOffsetUtc = schedule.ArrivalDayOffsetUtc,
                    origin = schedule.Origin,
                    destination = schedule.Destination,
                    aircraftType = schedule.AircraftType,
                    cabins = cabinsPayload
                });
            }
        }

        if (flightItems.Count == 0)
        {
            return new ImportSchedulesToInventoryResponse
            {
                SchedulesProcessed = schedulesWithConfig.Count,
                InventoriesCreated = 0,
                InventoriesSkipped = 0,
                FaresCreated = 0
            };
        }

        // 6. Batch-create inventory in Offer MS; existing records are skipped.
        var batchResult = await _offerClient.BatchCreateFlightsAsync(
            new { flights = flightItems }, cancellationToken);

        _logger.LogInformation(
            "ImportSchedulesToInventory: inventories created={Created}, skipped={Skipped}",
            batchResult.Created, batchResult.Skipped);

        // 7. Apply stored fare rules to each newly created inventory.
        var scheduleByFlight = schedulesWithConfig
            .ToDictionary(s => s.FlightNumber, StringComparer.OrdinalIgnoreCase);

        var faresCreated = 0;

        foreach (var inventory in batchResult.Inventories)
        {
            if (!scheduleByFlight.TryGetValue(inventory.FlightNumber, out var schedule))
                continue;

            // Apply fare rules for each cabin in this inventory.
            foreach (var cabin in inventory.Cabins)
            {
                if (!fareRulesByCabin.TryGetValue(cabin.CabinCode, out var rules))
                    continue;

                foreach (var rule in rules)
                {
                    // Skip rules scoped to a different flight number.
                    if (!string.IsNullOrEmpty(rule.FlightNumber) &&
                        !string.Equals(rule.FlightNumber, inventory.FlightNumber, StringComparison.OrdinalIgnoreCase))
                        continue;

                    try
                    {
                        var currencyCode = rule.CurrencyCode ?? "GBP";
                        var baseFareAmount = rule.MinAmount ?? 0m;
                        var taxAmount = rule.TaxAmount ?? 0m;
                        var pointsPrice = rule.RuleType == "Points" ? rule.MinPoints : null;
                        var pointsTaxes = rule.RuleType == "Points" ? rule.PointsTaxes : null;

                        var validFrom = rule.ValidFrom ?? schedule.ValidFrom;
                        var validTo = rule.ValidTo ?? schedule.ValidTo;

                        await _offerClient.CreateFareAsync(
                            inventoryId: inventory.InventoryId,
                            fareBasisCode: rule.FareBasisCode,
                            fareFamily: rule.FareFamily,
                            bookingClass: rule.BookingClass,
                            currencyCode: currencyCode,
                            baseFareAmount: baseFareAmount,
                            taxAmount: taxAmount,
                            isRefundable: rule.IsRefundable,
                            isChangeable: rule.IsChangeable,
                            changeFeeAmount: rule.ChangeFeeAmount,
                            cancellationFeeAmount: rule.CancellationFeeAmount,
                            pointsPrice: pointsPrice,
                            pointsTaxes: pointsTaxes,
                            validFrom: validFrom,
                            validTo: validTo,
                            cancellationToken: cancellationToken);

                        faresCreated++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex,
                            "Failed to create fare {FareBasisCode}/{CabinCode} for inventory {InventoryId} — skipping",
                            rule.FareBasisCode, cabin.CabinCode, inventory.InventoryId);
                    }
                }
            }
        }

        _logger.LogInformation(
            "ImportSchedulesToInventory complete: schedulesProcessed={Schedules}, " +
            "inventoriesCreated={Created}, inventoriesSkipped={Skipped}, faresCreated={Fares}",
            schedulesWithConfig.Count, batchResult.Created, batchResult.Skipped, faresCreated);

        return new ImportSchedulesToInventoryResponse
        {
            SchedulesProcessed = schedulesWithConfig.Count,
            InventoriesCreated = batchResult.Created,
            InventoriesSkipped = batchResult.Skipped,
            FaresCreated = faresCreated
        };
    }

    /// <summary>
    /// Returns operating dates within [from, validTo] that match the DaysOfWeek bitmask.
    /// Mon=1, Tue=2, Wed=4, Thu=8, Fri=16, Sat=32, Sun=64.
    /// </summary>
    private static IReadOnlyList<DateTime> GetOperatingDates(DateTime from, DateTime validTo, int daysOfWeek)
    {
        var dates = new List<DateTime>();
        for (var date = from.Date; date <= validTo.Date; date = date.AddDays(1))
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

            if ((daysOfWeek & dayBit) != 0)
                dates.Add(date);
        }
        return dates.AsReadOnly();
    }
}

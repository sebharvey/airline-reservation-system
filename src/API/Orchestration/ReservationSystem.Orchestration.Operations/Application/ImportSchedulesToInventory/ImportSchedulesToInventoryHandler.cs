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
///   2. For each schedule, enumerate operating dates from ValidFrom/ValidTo and DaysOfWeek bitmask.
///   3. For each operating date × cabin, build a batch inventory creation request.
///   4. Call Offer MS POST /v1/flights/batch — existing records are skipped automatically.
///   5. For each newly created inventory, call Offer MS POST /v1/flights/{id}/fares to attach fares.
///   6. Return a summary of schedules processed, inventories created/skipped, and fares created.
/// </summary>
public sealed class ImportSchedulesToInventoryHandler
{
    private readonly ScheduleServiceClient _scheduleClient;
    private readonly OfferServiceClient _offerClient;
    private readonly ILogger<ImportSchedulesToInventoryHandler> _logger;

    public ImportSchedulesToInventoryHandler(
        ScheduleServiceClient scheduleClient,
        OfferServiceClient offerClient,
        ILogger<ImportSchedulesToInventoryHandler> logger)
    {
        _scheduleClient = scheduleClient;
        _offerClient = offerClient;
        _logger = logger;
    }

    public async Task<ImportSchedulesToInventoryResponse> HandleAsync(
        ImportSchedulesToInventoryCommand command,
        CancellationToken cancellationToken = default)
    {
        // 1. Retrieve all persisted schedules from Schedule MS.
        var schedulesResult = await _scheduleClient.GetSchedulesAsync(cancellationToken);

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

        // 2. Build a batch payload: one entry per schedule × operating date × cabin.
        var flightItems = new List<object>();

        foreach (var schedule in schedulesResult.Schedules)
        {
            var validFrom = DateTime.Parse(schedule.ValidFrom);
            var validTo = DateTime.Parse(schedule.ValidTo);
            var operatingDates = GetOperatingDates(validFrom, validTo, schedule.DaysOfWeek);

            foreach (var date in operatingDates)
            {
                foreach (var cabin in command.Cabins)
                {
                    flightItems.Add(new
                    {
                        flightNumber = schedule.FlightNumber,
                        departureDate = date.ToString("yyyy-MM-dd"),
                        departureTime = schedule.DepartureTime,
                        arrivalTime = schedule.ArrivalTime,
                        arrivalDayOffset = schedule.ArrivalDayOffset,
                        origin = schedule.Origin,
                        destination = schedule.Destination,
                        aircraftType = schedule.AircraftType,
                        cabinCode = cabin.CabinCode,
                        totalSeats = cabin.TotalSeats
                    });
                }
            }
        }

        if (flightItems.Count == 0)
        {
            return new ImportSchedulesToInventoryResponse
            {
                SchedulesProcessed = schedulesResult.Count,
                InventoriesCreated = 0,
                InventoriesSkipped = 0,
                FaresCreated = 0
            };
        }

        // 3. Batch-create inventory in Offer MS; existing records are skipped.
        var batchResult = await _offerClient.BatchCreateFlightsAsync(
            new { flights = flightItems }, cancellationToken);

        _logger.LogInformation(
            "ImportSchedulesToInventory: inventories created={Created}, skipped={Skipped}",
            batchResult.Created, batchResult.Skipped);

        // 4. Create fares for each newly created inventory.
        // Build a lookup from flightNumber → schedule for fare validity dates.
        var scheduleByFlight = schedulesResult.Schedules
            .ToDictionary(s => s.FlightNumber, StringComparer.OrdinalIgnoreCase);

        var faresCreated = 0;

        foreach (var inventory in batchResult.Inventories)
        {
            var cabin = command.Cabins.FirstOrDefault(c =>
                string.Equals(c.CabinCode, inventory.CabinCode, StringComparison.OrdinalIgnoreCase));

            if (cabin is null) continue;

            if (!scheduleByFlight.TryGetValue(inventory.FlightNumber, out var schedule)) continue;

            foreach (var fare in cabin.Fares)
            {
                try
                {
                    await _offerClient.CreateFareAsync(
                        inventoryId: inventory.InventoryId,
                        fareBasisCode: fare.FareBasisCode,
                        fareFamily: fare.FareFamily,
                        bookingClass: fare.BookingClass,
                        currencyCode: fare.CurrencyCode,
                        baseFareAmount: fare.BaseFareAmount,
                        taxAmount: fare.TaxAmount,
                        isRefundable: fare.IsRefundable,
                        isChangeable: fare.IsChangeable,
                        changeFeeAmount: fare.ChangeFeeAmount,
                        cancellationFeeAmount: fare.CancellationFeeAmount,
                        pointsPrice: fare.PointsPrice,
                        pointsTaxes: fare.PointsTaxes,
                        validFrom: schedule.ValidFrom,
                        validTo: schedule.ValidTo,
                        cancellationToken: cancellationToken);

                    faresCreated++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Failed to create fare {FareBasisCode} for inventory {InventoryId} — skipping",
                        fare.FareBasisCode, inventory.InventoryId);
                }
            }
        }

        _logger.LogInformation(
            "ImportSchedulesToInventory complete: schedulesProcessed={Schedules}, " +
            "inventoriesCreated={Created}, inventoriesSkipped={Skipped}, faresCreated={Fares}",
            schedulesResult.Count, batchResult.Created, batchResult.Skipped, faresCreated);

        return new ImportSchedulesToInventoryResponse
        {
            SchedulesProcessed = schedulesResult.Count,
            InventoriesCreated = batchResult.Created,
            InventoriesSkipped = batchResult.Skipped,
            FaresCreated = faresCreated
        };
    }

    /// <summary>
    /// Returns the list of operating dates within [validFrom, validTo] that match the DaysOfWeek bitmask.
    /// Mon=1, Tue=2, Wed=4, Thu=8, Fri=16, Sat=32, Sun=64.
    /// </summary>
    private static IReadOnlyList<DateTime> GetOperatingDates(DateTime validFrom, DateTime validTo, int daysOfWeek)
    {
        var dates = new List<DateTime>();
        for (var date = validFrom.Date; date <= validTo.Date; date = date.AddDays(1))
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

using Microsoft.Extensions.Logging;
using ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Operations.Models.Requests;
using ReservationSystem.Orchestration.Operations.Models.Responses;

namespace ReservationSystem.Orchestration.Operations.Application.CreateSchedule;

public sealed class CreateScheduleHandler
{
    private readonly ScheduleServiceClient _scheduleServiceClient;
    private readonly OfferServiceClient _offerServiceClient;
    private readonly ILogger<CreateScheduleHandler> _logger;

    public CreateScheduleHandler(
        ScheduleServiceClient scheduleServiceClient,
        OfferServiceClient offerServiceClient,
        ILogger<CreateScheduleHandler> logger)
    {
        _scheduleServiceClient = scheduleServiceClient;
        _offerServiceClient = offerServiceClient;
        _logger = logger;
    }

    public async Task<CreateScheduleResponse> HandleAsync(CreateScheduleCommand command, CancellationToken cancellationToken)
    {
        // Step 1: Persist schedule via Schedule MS.
        var scheduleRequest = new
        {
            command.FlightNumber,
            command.Origin,
            command.Destination,
            command.DepartureTime,
            command.ArrivalTime,
            command.ArrivalDayOffset,
            command.DaysOfWeek,
            command.AircraftType,
            command.ValidFrom,
            command.ValidTo,
            createdBy = "operations-api"
        };

        var schedule = await _scheduleServiceClient.CreateScheduleAsync(scheduleRequest, cancellationToken);

        _logger.LogInformation(
            "Created schedule {ScheduleId} for {FlightNumber} ({Origin}-{Destination}) with {DateCount} operating dates",
            schedule.ScheduleId, command.FlightNumber, command.Origin, command.Destination, schedule.OperatingDates.Count);

        // Step 2: Generate FlightInventory and Fare records for each operating date and cabin.
        var inventoryCount = 0;

        foreach (var operatingDate in schedule.OperatingDates)
        {
            foreach (var cabin in command.Cabins)
            {
                // Create FlightInventory for this date + cabin.
                var flight = await _offerServiceClient.CreateFlightAsync(
                    command.FlightNumber,
                    operatingDate,
                    command.DepartureTime,
                    command.ArrivalTime,
                    command.ArrivalDayOffset,
                    command.Origin,
                    command.Destination,
                    command.AircraftType,
                    cabin.CabinCode,
                    cabin.TotalSeats,
                    cancellationToken);

                inventoryCount++;

                // Create Fare records for this inventory.
                foreach (var fare in cabin.Fares)
                {
                    await _offerServiceClient.CreateFareAsync(
                        flight.InventoryId,
                        fare.FareBasisCode,
                        fare.FareFamily,
                        bookingClass: null,
                        fare.CurrencyCode,
                        fare.BaseFareAmount,
                        fare.TaxAmount,
                        fare.IsRefundable,
                        fare.IsChangeable,
                        fare.ChangeFeeAmount,
                        fare.CancellationFeeAmount,
                        fare.PointsPrice,
                        fare.PointsTaxes,
                        command.ValidFrom,
                        command.ValidTo,
                        cancellationToken);
                }

                _logger.LogDebug(
                    "Created inventory {InventoryId} with {FareCount} fares for {FlightNumber} on {Date} cabin {Cabin}",
                    flight.InventoryId, cabin.Fares.Count, command.FlightNumber, operatingDate, cabin.CabinCode);
            }
        }

        // Step 3: Update the schedule with the flights created count.
        await _scheduleServiceClient.UpdateScheduleAsync(schedule.ScheduleId, inventoryCount, cancellationToken);

        _logger.LogInformation(
            "Schedule {ScheduleId} complete: {InventoryCount} flight inventory records created",
            schedule.ScheduleId, inventoryCount);

        return new CreateScheduleResponse
        {
            ScheduleId = schedule.ScheduleId,
            FlightsCreated = inventoryCount
        };
    }
}

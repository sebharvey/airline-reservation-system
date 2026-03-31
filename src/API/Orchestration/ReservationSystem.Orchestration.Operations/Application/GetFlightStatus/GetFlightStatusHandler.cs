using ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Operations.Models.Responses;

namespace ReservationSystem.Orchestration.Operations.Application.GetFlightStatus;

public sealed class GetFlightStatusHandler
{
    private readonly OfferServiceClient _offerServiceClient;

    public GetFlightStatusHandler(OfferServiceClient offerServiceClient)
    {
        _offerServiceClient = offerServiceClient;
    }

    public async Task<FlightStatusResponse?> HandleAsync(
        GetFlightStatusQuery query,
        CancellationToken ct = default)
    {
        var inventory = await _offerServiceClient.GetFlightInventoryAsync(
            query.FlightNumber, query.DepartureDate, ct);

        if (inventory is null)
            return null;

        var departureDateTime = $"{inventory.DepartureDate}T{inventory.DepartureTime}:00Z";
        var arrivalDate = inventory.ArrivalDayOffset > 0
            ? DateOnly.ParseExact(inventory.DepartureDate, "yyyy-MM-dd")
                      .AddDays(inventory.ArrivalDayOffset)
                      .ToString("yyyy-MM-dd")
            : inventory.DepartureDate;
        var arrivalDateTime = $"{arrivalDate}T{inventory.ArrivalTime}:00Z";

        var status = DeriveStatus(inventory.Status, inventory.TotalSeats, inventory.TotalSeatsAvailable);

        return new FlightStatusResponse
        {
            FlightNumber                = inventory.FlightNumber,
            Origin                      = inventory.Origin,
            Destination                 = inventory.Destination,
            ScheduledDepartureDateTime  = departureDateTime,
            ScheduledArrivalDateTime    = arrivalDateTime,
            EstimatedDepartureDateTime  = departureDateTime,
            EstimatedArrivalDateTime    = arrivalDateTime,
            Status                      = status,
            Gate                        = null,
            Terminal                    = null,
            AircraftType                = inventory.AircraftType,
            DelayMinutes                = 0,
            StatusMessage               = DeriveStatusMessage(status, inventory.LoadFactor)
        };
    }

    private static string DeriveStatus(string inventoryStatus, int totalSeats, int seatsAvailable)
    {
        if (string.Equals(inventoryStatus, "Cancelled", StringComparison.OrdinalIgnoreCase))
            return "Cancelled";

        return "OnTime";
    }

    private static string DeriveStatusMessage(string status, int loadFactor)
    {
        return status switch
        {
            "Cancelled" => "This flight has been cancelled",
            _ => "Flight is on time"
        };
    }
}

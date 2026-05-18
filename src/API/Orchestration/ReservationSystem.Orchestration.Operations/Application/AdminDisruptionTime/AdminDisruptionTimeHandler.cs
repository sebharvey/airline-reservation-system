using Microsoft.Extensions.Logging;
using ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Operations.Models.Responses;

namespace ReservationSystem.Orchestration.Operations.Application.AdminDisruptionTime;

public sealed class AdminDisruptionTimeHandler
{
    private readonly OfferServiceClient _offerClient;
    private readonly DeliveryServiceClient _deliveryClient;
    private readonly ILogger<AdminDisruptionTimeHandler> _logger;

    public AdminDisruptionTimeHandler(
        OfferServiceClient offerClient,
        DeliveryServiceClient deliveryClient,
        ILogger<AdminDisruptionTimeHandler> logger)
    {
        _offerClient    = offerClient;
        _deliveryClient = deliveryClient;
        _logger         = logger;
    }

    public async Task<AdminDisruptionTimeResponse> HandleAsync(
        AdminDisruptionTimeCommand command,
        CancellationToken ct)
    {
        _logger.LogInformation(
            "Processing flight delay for {FlightNumber}/{DepartureDate}: {OldDep} → {NewDep}",
            command.FlightNumber, command.DepartureDate, command.NewDepartureTime, command.NewArrivalTime);

        // 1. Update inventory times in Offer MS
        var inventoriesUpdated = await _offerClient.UpdateInventoryTimesAsync(
            command.FlightNumber,
            command.DepartureDate,
            command.NewDepartureTime,
            command.NewArrivalTime,
            command.NewArrivalDayOffset,
            command.NewDepartureTimeUtc,
            command.NewArrivalTimeUtc,
            command.NewArrivalDayOffsetUtc,
            ct);

        // 2. Update manifest flight times in Delivery MS (marks rows as Delayed)
        var affectedPassengers = await _deliveryClient.UpdateManifestFlightTimesAsync(
            command.FlightNumber,
            command.DepartureDate,
            command.NewDepartureTime,
            command.NewArrivalTime,
            ct);

        _logger.LogInformation(
            "Delay applied to {FlightNumber}/{DepartureDate}: {Inv} inventories updated, {Pax} passenger manifest rows updated",
            command.FlightNumber, command.DepartureDate, inventoriesUpdated, affectedPassengers);

        return new AdminDisruptionTimeResponse
        {
            FlightNumber          = command.FlightNumber,
            DepartureDate         = command.DepartureDate,
            NewDepartureTime      = command.NewDepartureTime,
            NewArrivalTime        = command.NewArrivalTime,
            NewDepartureTimeUtc   = command.NewDepartureTimeUtc,
            NewArrivalTimeUtc     = command.NewArrivalTimeUtc,
            InventoriesUpdated    = inventoriesUpdated,
            AffectedPassengerCount = affectedPassengers,
            ProcessedAt           = DateTime.UtcNow
        };
    }
}

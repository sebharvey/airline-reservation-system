using ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Operations.Models.Responses;

namespace ReservationSystem.Orchestration.Operations.Application.AdminDisruptionChange;

public sealed class AdminDisruptionChangeHandler
{
    private readonly OfferServiceClient _offerServiceClient;

    public AdminDisruptionChangeHandler(OfferServiceClient offerServiceClient)
    {
        _offerServiceClient = offerServiceClient;
    }

    public async Task<AdminDisruptionChangeResponse> HandleAsync(
        AdminDisruptionChangeCommand command,
        CancellationToken ct)
    {
        await _offerServiceClient.UpdateInventoryAircraftTypeAsync(
            command.FlightNumber,
            command.DepartureDate,
            command.NewAircraftType,
            ct);

        return new AdminDisruptionChangeResponse
        {
            FlightNumber = command.FlightNumber,
            DepartureDate = command.DepartureDate,
            NewAircraftType = command.NewAircraftType,
            ProcessedAt = DateTime.UtcNow
        };
    }
}

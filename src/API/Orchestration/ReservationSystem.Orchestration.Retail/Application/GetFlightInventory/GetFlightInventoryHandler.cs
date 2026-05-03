using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices.Dto;

namespace ReservationSystem.Orchestration.Retail.Application.GetFlightInventory;

public sealed class GetFlightInventoryHandler
{
    private readonly OfferServiceClient _offerServiceClient;

    public GetFlightInventoryHandler(OfferServiceClient offerServiceClient)
    {
        _offerServiceClient = offerServiceClient;
    }

    public async Task<FlightInventoryWithPinnedDto> HandleAsync(
        GetFlightInventoryQuery query,
        CancellationToken cancellationToken)
    {
        return await _offerServiceClient.GetFlightInventoryByDateAsync(
            query.DepartureDate, query.PinnedInventoryIds, cancellationToken);
    }
}

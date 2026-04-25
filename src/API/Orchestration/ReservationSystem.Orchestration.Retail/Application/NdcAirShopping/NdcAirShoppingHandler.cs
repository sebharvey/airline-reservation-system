using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices.Dto;

namespace ReservationSystem.Orchestration.Retail.Application.NdcAirShopping;

/// <summary>
/// Handles POST /v1/ndc/AirShopping.
/// Delegates to the Offer microservice using the same search path as the standard
/// Retail API slice search, then returns the raw offer result for XML serialisation.
/// </summary>
public sealed class NdcAirShoppingHandler
{
    private readonly OfferServiceClient _offerServiceClient;

    public NdcAirShoppingHandler(OfferServiceClient offerServiceClient)
    {
        _offerServiceClient = offerServiceClient;
    }

    public async Task<OfferSearchResultDto> HandleAsync(
        NdcAirShoppingCommand command,
        CancellationToken cancellationToken)
    {
        return await _offerServiceClient.SearchAsync(
            command.Origin,
            command.Destination,
            command.DepartureDate,
            command.TotalPaxCount,
            bookingType: "Revenue",
            includePrivateFares: false,
            customerContext: null,
            cancellationToken);
    }
}

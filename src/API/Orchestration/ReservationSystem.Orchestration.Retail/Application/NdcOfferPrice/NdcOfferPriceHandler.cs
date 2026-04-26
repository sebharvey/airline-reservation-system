using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices.Dto;

namespace ReservationSystem.Orchestration.Retail.Application.NdcOfferPrice;

public enum NdcOfferPriceOutcome { Success, NotFound, Expired }

public sealed record NdcOfferPriceResult(
    NdcOfferPriceOutcome Outcome,
    OfferDetailDto? OfferDetail = null,
    RepriceOfferDto? RepriceResult = null);

/// <summary>
/// Handles POST /v1/ndc/OfferPrice.
/// Validates and re-prices a stored offer from a prior AirShopping response by
/// calling the Offer microservice reprice endpoint, then fetches the flight detail
/// record needed to build the NDC DataLists in the response.
/// </summary>
public sealed class NdcOfferPriceHandler
{
    private readonly OfferServiceClient _offerServiceClient;

    public NdcOfferPriceHandler(OfferServiceClient offerServiceClient)
    {
        _offerServiceClient = offerServiceClient;
    }

    public async Task<NdcOfferPriceResult> HandleAsync(
        NdcOfferPriceCommand command,
        CancellationToken cancellationToken)
    {
        var reprice = await _offerServiceClient.RepriceOfferAsync(
            command.OfferRefId, cancellationToken: cancellationToken);

        if (reprice is null)
            return new NdcOfferPriceResult(NdcOfferPriceOutcome.NotFound);

        if (!reprice.Validated)
            return new NdcOfferPriceResult(NdcOfferPriceOutcome.Expired);

        var offerDetail = await _offerServiceClient.GetOfferAsync(
            command.OfferRefId, cancellationToken: cancellationToken);

        if (offerDetail is null)
            return new NdcOfferPriceResult(NdcOfferPriceOutcome.NotFound);

        return new NdcOfferPriceResult(NdcOfferPriceOutcome.Success, offerDetail, reprice);
    }
}

using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices.Dto;

namespace ReservationSystem.Orchestration.Retail.Application.NdcSeatAvailability;

public enum NdcSeatAvailabilityOutcome { Success, OfferNotFound, SeatmapNotFound }

public sealed record NdcSeatAvailabilityResult(
    NdcSeatAvailabilityOutcome Outcome,
    OfferDetailDto? OfferDetail = null,
    SeatmapLayoutDto? Seatmap = null,
    SeatOffersDto? SeatOffers = null);

/// <summary>
/// Handles POST /v1/ndc/SeatAvailability.
///
/// Resolves the stored offer to obtain the flight's InventoryId and AircraftType,
/// then fetches the physical seatmap layout and per-seat offer pricing concurrently.
/// Returns OfferNotFound when the OfferRefId does not match any stored offer, and
/// SeatmapNotFound when no seatmap is configured for the aircraft type.
/// </summary>
public sealed class NdcSeatAvailabilityHandler
{
    private readonly OfferServiceClient _offerServiceClient;
    private readonly SeatServiceClient _seatServiceClient;

    public NdcSeatAvailabilityHandler(
        OfferServiceClient offerServiceClient,
        SeatServiceClient seatServiceClient)
    {
        _offerServiceClient = offerServiceClient;
        _seatServiceClient = seatServiceClient;
    }

    public async Task<NdcSeatAvailabilityResult> HandleAsync(
        NdcSeatAvailabilityCommand command,
        CancellationToken cancellationToken)
    {
        var offerDetail = await _offerServiceClient.GetOfferAsync(
            command.OfferRefId, cancellationToken: cancellationToken);

        if (offerDetail is null)
            return new NdcSeatAvailabilityResult(NdcSeatAvailabilityOutcome.OfferNotFound);

        // Fetch seatmap layout and per-seat offers concurrently.
        var seatmapTask = _seatServiceClient.GetSeatmapAsync(
            offerDetail.AircraftType, cancellationToken);
        var seatOffersTask = _seatServiceClient.GetSeatOffersAsync(
            offerDetail.InventoryId, offerDetail.AircraftType, cancellationToken);

        await Task.WhenAll(seatmapTask, seatOffersTask);

        var seatmap = await seatmapTask;
        if (seatmap is null)
            return new NdcSeatAvailabilityResult(NdcSeatAvailabilityOutcome.SeatmapNotFound);

        // SeatOffers may be null when seat pricing is not configured — the seatmap
        // is still returned without pricing information.
        var seatOffers = await seatOffersTask;

        return new NdcSeatAvailabilityResult(
            NdcSeatAvailabilityOutcome.Success, offerDetail, seatmap, seatOffers);
    }
}

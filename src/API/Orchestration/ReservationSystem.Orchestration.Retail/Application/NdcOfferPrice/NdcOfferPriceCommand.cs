using ReservationSystem.Orchestration.Retail.Application.NdcAirShopping;

namespace ReservationSystem.Orchestration.Retail.Application.NdcOfferPrice;

/// <summary>
/// Parsed parameters extracted from an IATA NDC 21.3 OfferPriceRQ.
/// OfferRefId is the internal offerId GUID published in the preceding AirShoppingRS.
/// </summary>
public sealed record NdcOfferPriceCommand(
    Guid OfferRefId,
    string? OfferItemRefId,
    string? ShoppingResponseId,
    IReadOnlyList<NdcPassengerType>? Passengers);

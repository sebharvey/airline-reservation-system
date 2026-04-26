using ReservationSystem.Orchestration.Retail.Application.NdcAirShopping;

namespace ReservationSystem.Orchestration.Retail.Application.NdcServiceList;

/// <summary>
/// Parsed parameters from an IATA NDC 21.3 ServiceListRQ.
/// OfferRefId is optional — when present the handler resolves flight context from the
/// stored offer so SSR options can be filtered by cabin class and flight number.
/// NdcCabinCode is the NDC cabin code from the request (M=Economy, W=PremiumEconomy,
/// C=Business, F=First) and overrides the cabin derived from the offer if both are set.
/// </summary>
public sealed record NdcServiceListCommand(
    Guid? OfferRefId,
    string? NdcCabinCode,
    IReadOnlyList<NdcPassengerType>? Passengers);

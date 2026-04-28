namespace ReservationSystem.Orchestration.Retail.Application.NdcOrderCreate;

/// <summary>
/// A named passenger extracted from the NDC OrderCreateRQ Pax element.
/// PaxId maps to the NDC PaxID reference used throughout the request and response.
/// </summary>
public sealed record NdcOrderCreatePassenger(
    string PaxId,
    string Ptc,
    string GivenName,
    string Surname,
    string? Dob,
    string? GenderCode,
    string? Email,
    string? Phone);

/// <summary>
/// Card payment details extracted from the NDC OrderCreateRQ Payment/PaymentCard element.
/// </summary>
public sealed record NdcOrderCreatePaymentCard(
    string CardholderName,
    string CardNumber,
    string CardTypeCode,
    string ExpiryMonth,
    string ExpiryYear,
    string? Cvv);

/// <summary>
/// Parsed parameters from an IATA NDC 21.3 OrderCreateRQ.
/// OfferRefId identifies the stored offer GUID published in a prior AirShoppingRS or OfferPriceRS.
/// GdsBookingReference carries the upstream GDS record locator from Query/BookingReferences/BookingReference/ID.
/// </summary>
public sealed record NdcOrderCreateCommand(
    Guid OfferRefId,
    string? OfferItemRefId,
    string? ShoppingResponseId,
    IReadOnlyList<NdcOrderCreatePassenger> Passengers,
    NdcOrderCreatePaymentCard? PaymentCard,
    string? GdsBookingReference = null);

namespace ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices.Dto;

public sealed record SeatOffersDto(
    Guid FlightId,
    string AircraftType,
    List<SeatOfferDto> SeatOffers);

public sealed record SeatOfferDto(
    string SeatOfferId,
    string SeatNumber,
    string CabinCode,
    string Position,
    string Type,
    List<string> Attributes,
    bool IsSelectable,
    bool IsChargeable,
    decimal Price,
    decimal Tax,
    string CurrencyCode);

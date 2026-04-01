namespace ReservationSystem.Microservices.Ancillary.Application.Seat.GetSeatOffer;

/// <summary>
/// Query carrying the seat offer identifier needed to retrieve a single seat offer.
/// </summary>
public sealed record GetSeatOfferQuery(string SeatOfferId);

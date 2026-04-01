namespace ReservationSystem.Microservices.Ancillary.Application.Seat.GetSeatOffers;

/// <summary>
/// Query carrying the flight identifier needed to retrieve seat offers.
/// </summary>
public sealed record GetSeatOffersQuery(Guid FlightId);

namespace ReservationSystem.Microservices.Offer.Application.UseCases.GetOffer;

/// <summary>
/// Query to retrieve a single Offer by its identifier.
/// Immutable record — queries carry no side effects.
/// </summary>
public sealed record GetOfferQuery(Guid Id);

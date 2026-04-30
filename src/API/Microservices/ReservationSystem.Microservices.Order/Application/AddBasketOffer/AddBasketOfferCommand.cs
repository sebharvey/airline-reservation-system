namespace ReservationSystem.Microservices.Order.Application.AddBasketOffer;

/// <summary>
/// Command to add a flight offer to an existing basket.
/// </summary>
public sealed record AddBasketOfferCommand(Guid BasketId, string OfferJson);

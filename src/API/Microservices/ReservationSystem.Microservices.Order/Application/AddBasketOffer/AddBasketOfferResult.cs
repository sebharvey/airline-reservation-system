namespace ReservationSystem.Microservices.Order.Application.AddBasketOffer;

/// <summary>
/// Result returned after successfully adding a flight offer to a basket.
/// </summary>
public sealed record AddBasketOfferResult(Guid BasketId, string BasketItemId, decimal TotalFareAmount, decimal TotalAmount);

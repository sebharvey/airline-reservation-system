namespace ReservationSystem.Microservices.Order.Models.Responses;

public sealed class AddBasketOfferResponse
{
    public Guid BasketId { get; init; }
    public string BasketItemId { get; init; } = string.Empty;
    public decimal TotalFareAmount { get; init; }
    public decimal TotalAmount { get; init; }
}

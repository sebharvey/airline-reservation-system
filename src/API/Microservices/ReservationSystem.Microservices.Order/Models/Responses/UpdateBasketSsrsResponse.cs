namespace ReservationSystem.Microservices.Order.Models.Responses;

public sealed class UpdateBasketSsrsResponse
{
    public Guid BasketId { get; init; }
    public int SsrCount { get; init; }
    public decimal TotalAmount { get; init; }
}

namespace ReservationSystem.Microservices.Order.Models.Responses;

public sealed class UpdateBasketAmountResponse
{
    public Guid BasketId { get; init; }
    public decimal? TotalSeatAmount { get; init; }
    public decimal TotalAmount { get; init; }
}

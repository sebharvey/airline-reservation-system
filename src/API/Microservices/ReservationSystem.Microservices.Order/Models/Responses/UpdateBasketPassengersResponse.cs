namespace ReservationSystem.Microservices.Order.Models.Responses;

public sealed class UpdateBasketPassengersResponse
{
    public Guid BasketId { get; init; }
    public int PassengerCount { get; init; }
}

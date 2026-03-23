namespace ReservationSystem.Microservices.Order.Models.Responses;

public sealed class OrderStatusResponse
{
    public string BookingReference { get; init; } = string.Empty;
    public string OrderStatus { get; init; } = string.Empty;
    public int Version { get; init; }
}

namespace ReservationSystem.Microservices.Order.Models.Requests;

public sealed class RetrieveOrderRequest
{
    public string BookingReference { get; init; } = string.Empty;
    public string Surname { get; init; } = string.Empty;
}

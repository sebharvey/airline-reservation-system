namespace ReservationSystem.Microservices.Order.Models.Responses;

public sealed class CheckInResponse
{
    public string BookingReference { get; init; } = string.Empty;
    public int CheckedInPassengers { get; init; }
}

namespace ReservationSystem.Microservices.Customer.Models.Requests;

public sealed class AddCustomerOrderRequest
{
    public Guid OrderId { get; init; }
    public string BookingReference { get; init; } = string.Empty;
}

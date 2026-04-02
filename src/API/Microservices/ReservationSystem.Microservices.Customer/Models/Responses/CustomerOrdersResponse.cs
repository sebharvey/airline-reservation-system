namespace ReservationSystem.Microservices.Customer.Models.Responses;

public sealed class CustomerOrdersResponse
{
    public string LoyaltyNumber { get; init; } = string.Empty;
    public IReadOnlyList<CustomerOrderItem> Orders { get; init; } = Array.Empty<CustomerOrderItem>();
}

public sealed class CustomerOrderItem
{
    public Guid CustomerOrderId { get; init; }
    public Guid OrderId { get; init; }
    public string BookingReference { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}

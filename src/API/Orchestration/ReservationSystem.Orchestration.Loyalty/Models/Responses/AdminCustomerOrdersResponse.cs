namespace ReservationSystem.Orchestration.Loyalty.Models.Responses;

public sealed class AdminCustomerOrdersResponse
{
    public string LoyaltyNumber { get; init; } = string.Empty;
    public IReadOnlyList<AdminCustomerOrderItem> Orders { get; init; } = [];
}

public sealed class AdminCustomerOrderItem
{
    public Guid CustomerOrderId { get; init; }
    public Guid OrderId { get; init; }
    public string BookingReference { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}

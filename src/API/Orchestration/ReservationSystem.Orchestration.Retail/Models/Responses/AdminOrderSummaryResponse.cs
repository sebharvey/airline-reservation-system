namespace ReservationSystem.Orchestration.Retail.Models.Responses;

public sealed class AdminOrderSummaryResponse
{
    public string BookingReference { get; init; } = string.Empty;
    public string OrderStatus { get; init; } = string.Empty;
    public string ChannelCode { get; init; } = string.Empty;
    public string Currency { get; init; } = string.Empty;
    public decimal? TotalAmount { get; init; }
    public DateTime CreatedAt { get; init; }
    public string LeadPassengerName { get; init; } = string.Empty;
    public string Route { get; init; } = string.Empty;
}

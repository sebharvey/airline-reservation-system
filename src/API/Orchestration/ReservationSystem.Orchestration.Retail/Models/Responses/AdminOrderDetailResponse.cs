using System.Text.Json;

namespace ReservationSystem.Orchestration.Retail.Models.Responses;

public sealed class AdminOrderDetailResponse
{
    public Guid OrderId { get; init; }
    public string BookingReference { get; init; } = string.Empty;
    public string OrderStatus { get; init; } = string.Empty;
    public string ChannelCode { get; init; } = string.Empty;
    public string Currency { get; init; } = string.Empty;
    public decimal? TotalAmount { get; init; }
    public DateTime? TicketingTimeLimit { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public int Version { get; init; }
    public JsonElement? OrderData { get; init; }
}

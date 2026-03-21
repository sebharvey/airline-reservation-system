namespace ReservationSystem.Microservices.Order.Domain.Entities;

/// <summary>
/// Core domain entity representing a confirmed order (booking).
/// Created from a confirmed basket; identified externally by BookingReference.
/// </summary>
public sealed class Order
{
    public Guid OrderId { get; private set; }
    public string? BookingReference { get; private set; }
    public string OrderStatus { get; private set; } = string.Empty;
    public string ChannelCode { get; private set; } = string.Empty;
    public string CurrencyCode { get; private set; } = string.Empty;
    public DateTimeOffset? TicketingTimeLimit { get; private set; }
    public decimal? TotalAmount { get; private set; }
    public int Version { get; private set; }
    public string OrderData { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private Order() { }

    public static Order Create(
        string channelCode,
        string currencyCode,
        decimal? totalAmount,
        string orderData = "{}")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channelCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(currencyCode);

        return new Order
        {
            OrderId = Guid.NewGuid(),
            BookingReference = null,
            OrderStatus = OrderStatusValues.Pending,
            ChannelCode = channelCode,
            CurrencyCode = currencyCode,
            TicketingTimeLimit = null,
            TotalAmount = totalAmount,
            Version = 1,
            OrderData = orderData,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    public static Order Reconstitute(
        Guid orderId,
        string? bookingReference,
        string orderStatus,
        string channelCode,
        string currencyCode,
        DateTimeOffset? ticketingTimeLimit,
        decimal? totalAmount,
        int version,
        string orderData,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        return new Order
        {
            OrderId = orderId,
            BookingReference = bookingReference,
            OrderStatus = orderStatus,
            ChannelCode = channelCode,
            CurrencyCode = currencyCode,
            TicketingTimeLimit = ticketingTimeLimit,
            TotalAmount = totalAmount,
            Version = version,
            OrderData = orderData,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
    }

    public void Cancel()
    {
        OrderStatus = OrderStatusValues.Cancelled;
        UpdatedAt = DateTimeOffset.UtcNow;
        Version++;
    }
}

public static class OrderStatusValues
{
    public const string Pending = "pending";
    public const string Confirmed = "confirmed";
    public const string Cancelled = "cancelled";
    public const string Changed = "changed";
    public const string Rebooked = "rebooked";
}

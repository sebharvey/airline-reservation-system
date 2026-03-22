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
    public DateTime? TicketingTimeLimit { get; private set; }
    public decimal? TotalAmount { get; private set; }
    public int Version { get; private set; }
    public string OrderData { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

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
            OrderStatus = OrderStatusValues.Draft,
            ChannelCode = channelCode,
            CurrencyCode = currencyCode,
            TicketingTimeLimit = null,
            TotalAmount = totalAmount,
            Version = 1,
            OrderData = orderData,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public static Order Reconstitute(
        Guid orderId,
        string? bookingReference,
        string orderStatus,
        string channelCode,
        string currencyCode,
        DateTime? ticketingTimeLimit,
        decimal? totalAmount,
        int version,
        string orderData,
        DateTime createdAt,
        DateTime updatedAt)
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

    public void SetBookingReference(string bookingReference)
    {
        BookingReference = bookingReference;
    }

    public void Confirm(string bookingReference, decimal totalAmount, string orderData, DateTime? ticketingTimeLimit)
    {
        BookingReference = bookingReference;
        OrderStatus = OrderStatusValues.Confirmed;
        TotalAmount = totalAmount;
        OrderData = orderData;
        TicketingTimeLimit = ticketingTimeLimit;
        UpdatedAt = DateTime.UtcNow;
        Version++;
    }

    public void UpdateOrderData(string orderData)
    {
        OrderData = orderData;
        UpdatedAt = DateTime.UtcNow;
        Version++;
    }

    public void MarkChanged(string orderData)
    {
        OrderStatus = OrderStatusValues.Changed;
        OrderData = orderData;
        UpdatedAt = DateTime.UtcNow;
        Version++;
    }

    public void Cancel()
    {
        OrderStatus = OrderStatusValues.Cancelled;
        UpdatedAt = DateTime.UtcNow;
        Version++;
    }

    public void CancelWithData(string orderData)
    {
        OrderStatus = OrderStatusValues.Cancelled;
        OrderData = orderData;
        UpdatedAt = DateTime.UtcNow;
        Version++;
    }

    public bool IsMutable => OrderStatus is OrderStatusValues.Confirmed or OrderStatusValues.Changed;
}

public static class OrderStatusValues
{
    public const string Draft = "Draft";
    public const string OrderInit = "OrderInit";
    public const string Confirmed = "Confirmed";
    public const string Changed = "Changed";
    public const string Cancelled = "Cancelled";
}

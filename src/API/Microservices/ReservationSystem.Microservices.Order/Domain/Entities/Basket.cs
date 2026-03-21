namespace ReservationSystem.Microservices.Order.Domain.Entities;

/// <summary>
/// Core domain entity representing a shopping basket.
/// A basket holds the traveller's flight/seat/bag selections before confirmation.
/// </summary>
public sealed class Basket
{
    public Guid BasketId { get; private set; }
    public string ChannelCode { get; private set; } = string.Empty;
    public string CurrencyCode { get; private set; } = string.Empty;
    public string BasketStatus { get; private set; } = string.Empty;
    public decimal? TotalFareAmount { get; private set; }
    public decimal TotalSeatAmount { get; private set; }
    public decimal TotalBagAmount { get; private set; }
    public decimal? TotalAmount { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public Guid? ConfirmedOrderId { get; private set; }
    public int Version { get; private set; }
    public string BasketData { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private Basket() { }

    public static Basket Create(
        string channelCode,
        string currencyCode,
        DateTimeOffset expiresAt,
        string basketData = "{}")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channelCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(currencyCode);

        return new Basket
        {
            BasketId = Guid.NewGuid(),
            ChannelCode = channelCode,
            CurrencyCode = currencyCode,
            BasketStatus = BasketStatusValues.Open,
            TotalFareAmount = null,
            TotalSeatAmount = 0m,
            TotalBagAmount = 0m,
            TotalAmount = null,
            ExpiresAt = expiresAt,
            ConfirmedOrderId = null,
            Version = 1,
            BasketData = basketData,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    public static Basket Reconstitute(
        Guid basketId,
        string channelCode,
        string currencyCode,
        string basketStatus,
        decimal? totalFareAmount,
        decimal totalSeatAmount,
        decimal totalBagAmount,
        decimal? totalAmount,
        DateTimeOffset expiresAt,
        Guid? confirmedOrderId,
        int version,
        string basketData,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        return new Basket
        {
            BasketId = basketId,
            ChannelCode = channelCode,
            CurrencyCode = currencyCode,
            BasketStatus = basketStatus,
            TotalFareAmount = totalFareAmount,
            TotalSeatAmount = totalSeatAmount,
            TotalBagAmount = totalBagAmount,
            TotalAmount = totalAmount,
            ExpiresAt = expiresAt,
            ConfirmedOrderId = confirmedOrderId,
            Version = version,
            BasketData = basketData,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
    }

    public void Expire()
    {
        BasketStatus = BasketStatusValues.Expired;
        UpdatedAt = DateTimeOffset.UtcNow;
        Version++;
    }

    public void Confirm(Guid orderId)
    {
        BasketStatus = BasketStatusValues.Confirmed;
        ConfirmedOrderId = orderId;
        UpdatedAt = DateTimeOffset.UtcNow;
        Version++;
    }
}

public static class BasketStatusValues
{
    public const string Open = "open";
    public const string Expired = "expired";
    public const string Confirmed = "confirmed";
}

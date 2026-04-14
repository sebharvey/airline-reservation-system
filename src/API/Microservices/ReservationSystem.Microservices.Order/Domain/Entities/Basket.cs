namespace ReservationSystem.Microservices.Order.Domain.Entities;

/// <summary>
/// Core domain entity representing a shopping basket.
/// A basket holds the traveller's flight/seat/bag selections before confirmation.
/// </summary>
public sealed class Basket
{
    public Guid BasketId { get; private set; }
    public string CurrencyCode { get; private set; } = string.Empty;
    public string BasketStatus { get; private set; } = string.Empty;
    public decimal? TotalFareAmount { get; private set; }
    public decimal TotalSeatAmount { get; private set; }
    public decimal TotalBagAmount { get; private set; }
    public decimal? TotalAmount { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public Guid? ConfirmedOrderId { get; private set; }
    public int Version { get; private set; }
    public string BasketData { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private Basket() { }

    public static Basket Create(
        string currencyCode,
        DateTime expiresAt,
        string basketData = "{}")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currencyCode);

        return new Basket
        {
            BasketId = Guid.NewGuid(),
            CurrencyCode = currencyCode,
            BasketStatus = BasketStatusValues.Active,
            TotalFareAmount = null,
            TotalSeatAmount = 0m,
            TotalBagAmount = 0m,
            TotalAmount = null,
            ExpiresAt = expiresAt,
            ConfirmedOrderId = null,
            Version = 1,
            BasketData = basketData,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public static Basket Reconstitute(
        Guid basketId,
        string currencyCode,
        string basketStatus,
        decimal? totalFareAmount,
        decimal totalSeatAmount,
        decimal totalBagAmount,
        decimal? totalAmount,
        DateTime expiresAt,
        Guid? confirmedOrderId,
        int version,
        string basketData,
        DateTime createdAt,
        DateTime updatedAt)
    {
        return new Basket
        {
            BasketId = basketId,
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

    public void UpdateBasketData(string basketData)
    {
        BasketData = basketData;
        UpdatedAt = DateTime.UtcNow;
        Version++;
    }

    public void UpdateTotals(decimal? totalFareAmount, decimal totalSeatAmount, decimal totalBagAmount)
    {
        TotalFareAmount = totalFareAmount;
        TotalSeatAmount = totalSeatAmount;
        TotalBagAmount = totalBagAmount;
        TotalAmount = (totalFareAmount ?? 0m) + totalSeatAmount + totalBagAmount;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Expire()
    {
        BasketStatus = BasketStatusValues.Expired;
        UpdatedAt = DateTime.UtcNow;
        Version++;
    }

    public void Confirm(Guid orderId)
    {
        BasketStatus = BasketStatusValues.Confirmed;
        ConfirmedOrderId = orderId;
        UpdatedAt = DateTime.UtcNow;
        Version++;
    }

    public bool IsActive => BasketStatus == BasketStatusValues.Active;
    public bool IsExpired => ExpiresAt <= DateTime.UtcNow || BasketStatus == BasketStatusValues.Expired;
}

public static class BasketStatusValues
{
    public const string Active = "Active";
    public const string Expired = "Expired";
    public const string Abandoned = "Abandoned";
    public const string Confirmed = "Confirmed";
}

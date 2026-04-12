namespace ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices.Dto;

public sealed class TaxLineDto
{
    public string Code { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string? Description { get; init; }
}

public sealed class RepricedOfferItemDto
{
    public Guid OfferId { get; init; }
    public string CabinCode { get; init; } = string.Empty;
    public string FareBasisCode { get; init; } = string.Empty;
    public string? FareFamily { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public decimal BaseFareAmount { get; init; }
    public decimal TaxAmount { get; init; }
    public decimal TotalAmount { get; init; }
    public IReadOnlyList<TaxLineDto>? TaxLines { get; init; }
    public bool IsRefundable { get; init; }
    public bool IsChangeable { get; init; }
    public int SeatsAvailable { get; init; }
    public string BookingType { get; init; } = string.Empty;
}

public sealed class RepriceOfferDto
{
    public Guid StoredOfferId { get; init; }
    public Guid SessionId { get; init; }
    public Guid InventoryId { get; init; }
    public bool Validated { get; init; }
    public IReadOnlyList<RepricedOfferItemDto> Offers { get; init; } = [];
}

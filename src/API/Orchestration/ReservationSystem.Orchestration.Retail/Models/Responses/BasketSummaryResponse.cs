namespace ReservationSystem.Orchestration.Retail.Models.Responses;

public sealed class SummaryTaxLine
{
    public string Code { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string? Description { get; init; }
}

public sealed class SummaryFlight
{
    public Guid OfferId { get; init; }
    public Guid? SessionId { get; init; }
    public Guid? InventoryId { get; init; }
    public string FlightNumber { get; init; } = string.Empty;
    public string Origin { get; init; } = string.Empty;
    public string Destination { get; init; } = string.Empty;
    public string DepartureDate { get; init; } = string.Empty;
    public string DepartureTime { get; init; } = string.Empty;
    public string ArrivalTime { get; init; } = string.Empty;
    public string CabinCode { get; init; } = string.Empty;
    public string? FareFamily { get; init; }
    public bool Validated { get; init; }
    public decimal BaseFareAmount { get; init; }
    public decimal TaxAmount { get; init; }
    public decimal TotalAmount { get; init; }
    public IReadOnlyList<SummaryTaxLine>? TaxLines { get; init; }
}

public sealed class BasketSummaryResponse
{
    public Guid BasketId { get; init; }
    public string Status { get; init; } = string.Empty;
    public string Currency { get; init; } = string.Empty;
    public string? ExpiresAt { get; init; }
    public IReadOnlyList<SummaryFlight> Flights { get; init; } = [];
    public decimal TotalFareAmount { get; init; }
    public decimal TotalSeatAmount { get; init; }
    public decimal TotalBagAmount { get; init; }
    public decimal TotalPrice { get; init; }
}

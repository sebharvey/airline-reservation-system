namespace ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices.Dto;

public sealed class SalesPerformanceResponse
{
    public string InventoryId { get; init; } = string.Empty;
    public string Currency { get; init; } = string.Empty;
    public int TotalDays { get; init; }
    public IReadOnlyList<SalesPerformanceDayDto> Days { get; init; } = [];
}

public sealed class SalesPerformanceDayDto
{
    public string Date { get; init; } = string.Empty;
    public int OrderCount { get; init; }
    public int PassengerCount { get; init; }
    public decimal Revenue { get; init; }
    public decimal AncillaryRevenue { get; init; }
}

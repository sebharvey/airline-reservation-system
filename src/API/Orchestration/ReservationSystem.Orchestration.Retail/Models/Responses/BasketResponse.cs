namespace ReservationSystem.Orchestration.Retail.Models.Responses;

public sealed class BasketResponse
{
    public Guid BasketId { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? BookingType { get; init; }
    public string? CustomerId { get; init; }
    public IReadOnlyList<BasketFlight> Flights { get; init; } = [];
    public decimal? TotalFareAmount { get; init; }
    public decimal TotalSeatAmount { get; init; }
    public decimal TotalBagAmount { get; init; }
    public decimal TotalPrice { get; init; }
    public int? TotalPointsAmount { get; init; }
    public string Currency { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime? ExpiresAt { get; init; }
}

public sealed class BasketFlight
{
    public Guid OfferId { get; init; }
    public string FlightNumber { get; init; } = string.Empty;
    public string Origin { get; init; } = string.Empty;
    public string Destination { get; init; } = string.Empty;
    public DateTime DepartureDateTime { get; init; }
    public DateTime? ArrivalDateTime { get; init; }
    public string CabinCode { get; init; } = string.Empty;
    public string? FareFamily { get; init; }
    public decimal TotalAmount { get; init; }
}

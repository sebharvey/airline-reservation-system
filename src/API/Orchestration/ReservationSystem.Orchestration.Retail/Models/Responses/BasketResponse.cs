namespace ReservationSystem.Orchestration.Retail.Models.Responses;

public sealed class BasketResponse
{
    public Guid BasketId { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? CustomerId { get; init; }
    public IReadOnlyList<BasketFlight> Flights { get; init; } = [];
    public IReadOnlyList<BasketPassenger> Passengers { get; init; } = [];
    public decimal TotalPrice { get; init; }
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
    public DateTime DepartureTime { get; init; }
    public string CabinClass { get; init; } = string.Empty;
    public decimal Price { get; init; }
}

public sealed class BasketPassenger
{
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string PassengerType { get; init; } = string.Empty;
}

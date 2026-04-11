namespace ReservationSystem.Orchestration.Retail.Models.Responses;

public sealed class OrderResponse
{
    public string BookingReference { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string CustomerId { get; init; } = string.Empty;
    public IReadOnlyList<OrderFlight> Flights { get; init; } = [];
    public IReadOnlyList<OrderPassenger> Passengers { get; init; } = [];
    public IReadOnlyList<IssuedETicket> ETickets { get; init; } = [];
    public decimal TotalPrice { get; init; }
    public string Currency { get; init; } = string.Empty;
    public DateTime BookedAt { get; init; }
}

public sealed class IssuedETicket
{
    public string PassengerId { get; init; } = string.Empty;
    public List<string> SegmentIds { get; init; } = [];
    public string ETicketNumber { get; init; } = string.Empty;
}

public sealed class OrderFlight
{
    public string FlightNumber { get; init; } = string.Empty;
    public string Origin { get; init; } = string.Empty;
    public string Destination { get; init; } = string.Empty;
    public DateTime DepartureTime { get; init; }
    public DateTime ArrivalTime { get; init; }
    public string CabinClass { get; init; } = string.Empty;
}

public sealed class OrderPassenger
{
    public string PaxId { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string? SeatNumber { get; init; }
    public IReadOnlyList<string> BagAllowances { get; init; } = [];
}

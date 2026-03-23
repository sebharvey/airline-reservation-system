namespace ReservationSystem.Microservices.Offer.Models.Responses;

public sealed class SeatAvailabilityItem
{
    public Guid SeatOfferId { get; init; }
    public string SeatNumber { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
}

public sealed class SeatAvailabilityResponse
{
    public Guid FlightId { get; init; }
    public string FlightNumber { get; init; } = string.Empty;
    public string DepartureDate { get; init; } = string.Empty;
    public string CabinCode { get; init; } = string.Empty;
    public IReadOnlyList<SeatAvailabilityItem> SeatAvailability { get; init; } = [];
}

public sealed class ReserveSeatResponse
{
    public Guid FlightId { get; init; }
    public IReadOnlyList<string> Reserved { get; init; } = [];
}

public sealed class UpdateSeatStatusResponse
{
    public int Updated { get; init; }
}

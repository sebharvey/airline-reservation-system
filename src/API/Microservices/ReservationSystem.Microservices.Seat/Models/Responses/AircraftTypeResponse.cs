namespace ReservationSystem.Microservices.Seat.Models.Responses;

/// <summary>
/// HTTP response body representing an aircraft type resource.
/// </summary>
public sealed class AircraftTypeResponse
{
    public string AircraftTypeCode { get; init; } = string.Empty;
    public string Manufacturer { get; init; } = string.Empty;
    public string? FriendlyName { get; init; }
    public int TotalSeats { get; init; }
    public Dictionary<string, int>? CabinCounts { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

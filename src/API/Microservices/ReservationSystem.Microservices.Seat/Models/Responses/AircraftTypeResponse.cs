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
    public bool IsActive { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}

namespace ReservationSystem.Microservices.Seat.Models.Responses;

/// <summary>
/// HTTP response body representing a seatmap resource.
/// </summary>
public sealed class SeatmapResponse
{
    public Guid SeatmapId { get; init; }
    public string AircraftType { get; init; } = string.Empty;
    public int Version { get; init; }
    public int TotalSeats { get; init; }
    public string CabinLayout { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}

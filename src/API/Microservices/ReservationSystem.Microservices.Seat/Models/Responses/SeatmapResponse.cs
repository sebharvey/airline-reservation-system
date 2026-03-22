namespace ReservationSystem.Microservices.Seat.Models.Responses;

/// <summary>
/// HTTP response body representing a full seatmap resource (for GET /v1/seatmap/{aircraftType} and GET /v1/seatmaps/{seatmapId}).
/// </summary>
public sealed class SeatmapResponse
{
    public Guid SeatmapId { get; init; }
    public string AircraftType { get; init; } = string.Empty;
    public int Version { get; init; }
    public bool IsActive { get; init; }
    public int TotalSeats { get; init; }
    public string CabinLayout { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

/// <summary>
/// HTTP response body for seatmap list items (GET /v1/seatmaps).
/// Per spec, CabinLayout is NOT returned in the list response.
/// Uses aircraftTypeCode field name per admin endpoint spec.
/// </summary>
public sealed class SeatmapListItemResponse
{
    public Guid SeatmapId { get; init; }
    public string AircraftTypeCode { get; init; } = string.Empty;
    public int Version { get; init; }
    public bool IsActive { get; init; }
    public int TotalSeats { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

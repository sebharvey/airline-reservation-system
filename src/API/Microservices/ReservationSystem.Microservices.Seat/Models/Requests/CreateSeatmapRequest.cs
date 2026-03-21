namespace ReservationSystem.Microservices.Seat.Models.Requests;

/// <summary>
/// HTTP request body for creating/uploading a new seatmap.
/// </summary>
public sealed class CreateSeatmapRequest
{
    public string AircraftTypeCode { get; init; } = string.Empty;
    public string CabinLayout { get; init; } = string.Empty;
}

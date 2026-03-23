namespace ReservationSystem.Microservices.Offer.Models.Requests;

public sealed class UpdateSeatStatusRequest
{
    public IReadOnlyList<SeatStatusUpdateItem> Updates { get; init; } = [];
}

public sealed class SeatStatusUpdateItem
{
    public string SeatNumber { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
}

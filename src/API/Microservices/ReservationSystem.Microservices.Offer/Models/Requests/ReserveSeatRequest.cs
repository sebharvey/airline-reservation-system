namespace ReservationSystem.Microservices.Offer.Models.Requests;

public sealed class ReserveSeatRequest
{
    public IReadOnlyList<string> SeatNumbers { get; init; } = [];
    public Guid BasketId { get; init; }
}

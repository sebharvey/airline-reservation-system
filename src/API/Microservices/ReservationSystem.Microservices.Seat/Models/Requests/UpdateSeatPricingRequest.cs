namespace ReservationSystem.Microservices.Seat.Models.Requests;

/// <summary>
/// HTTP request body for updating an existing seat pricing rule.
/// </summary>
public sealed class UpdateSeatPricingRequest
{
    public decimal Price { get; init; }
    public bool IsActive { get; init; }
    public DateTimeOffset ValidFrom { get; init; }
    public DateTimeOffset? ValidTo { get; init; }
}

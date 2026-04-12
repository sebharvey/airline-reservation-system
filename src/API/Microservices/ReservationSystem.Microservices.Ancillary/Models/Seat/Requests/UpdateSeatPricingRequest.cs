namespace ReservationSystem.Microservices.Ancillary.Models.Seat.Requests;

/// <summary>
/// HTTP request body for updating an existing seat pricing rule.
/// </summary>
public sealed class UpdateSeatPricingRequest
{
    public string? CabinCode { get; init; }
    public string? Description { get; init; }
    public int? Sequence { get; init; }
    public string? CurrencyCode { get; init; }
    public decimal? Price { get; init; }
    public bool? IsActive { get; init; }
    public DateTime? ValidFrom { get; init; }
    public DateTime? ValidTo { get; init; }
}

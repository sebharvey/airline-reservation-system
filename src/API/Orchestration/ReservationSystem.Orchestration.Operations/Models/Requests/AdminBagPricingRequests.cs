namespace ReservationSystem.Orchestration.Operations.Models.Requests;

public sealed class AdminCreateBagPricingRequest
{
    public int BagSequence { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public string ValidFrom { get; init; } = string.Empty;
    public string? ValidTo { get; init; }
}

public sealed class AdminUpdateBagPricingRequest
{
    public string? CurrencyCode { get; init; }
    public decimal Price { get; init; }
    public bool IsActive { get; init; }
    public string ValidFrom { get; init; } = string.Empty;
    public string? ValidTo { get; init; }
}

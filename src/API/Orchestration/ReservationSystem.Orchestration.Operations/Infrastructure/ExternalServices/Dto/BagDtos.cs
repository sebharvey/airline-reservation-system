namespace ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices.Dto;

public sealed class BagPolicyDto
{
    public Guid PolicyId { get; init; }
    public string CabinCode { get; init; } = string.Empty;
    public int FreeBagsIncluded { get; init; }
    public int MaxWeightKgPerBag { get; init; }
    public bool IsActive { get; init; }
    public string CreatedAt { get; init; } = string.Empty;
    public string UpdatedAt { get; init; } = string.Empty;
}

public sealed class BagPoliciesListDto
{
    public IReadOnlyList<BagPolicyDto> Policies { get; init; } = [];
}

public sealed class BagPricingDto
{
    public Guid PricingId { get; init; }
    public int BagSequence { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public bool IsActive { get; init; }
    public string ValidFrom { get; init; } = string.Empty;
    public string? ValidTo { get; init; }
    public string CreatedAt { get; init; } = string.Empty;
    public string UpdatedAt { get; init; } = string.Empty;
}

public sealed class BagPricingListDto
{
    public IReadOnlyList<BagPricingDto> Pricing { get; init; } = [];
}

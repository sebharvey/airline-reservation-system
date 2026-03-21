namespace ReservationSystem.Microservices.Bags.Domain.Entities;

/// <summary>
/// Core domain entity representing a bag policy for a cabin class.
/// Defines the free bag allowance and maximum weight per bag.
/// </summary>
public sealed class BagPolicy
{
    public Guid PolicyId { get; private set; }
    public string CabinCode { get; private set; } = string.Empty;
    public int FreeBagsIncluded { get; private set; }
    public int MaxWeightKgPerBag { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private BagPolicy() { }

    /// <summary>
    /// Factory method for creating a brand-new bag policy. Assigns a new Id and timestamps.
    /// </summary>
    public static BagPolicy Create(string cabinCode, int freeBagsIncluded, int maxWeightKgPerBag)
    {
        return new BagPolicy
        {
            PolicyId = Guid.NewGuid(),
            CabinCode = cabinCode,
            FreeBagsIncluded = freeBagsIncluded,
            MaxWeightKgPerBag = maxWeightKgPerBag,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Factory method for reconstituting a bag policy from a persistence store.
    /// Does not assign a new Id or reset timestamps.
    /// </summary>
    public static BagPolicy Reconstitute(
        Guid policyId,
        string cabinCode,
        int freeBagsIncluded,
        int maxWeightKgPerBag,
        bool isActive,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        return new BagPolicy
        {
            PolicyId = policyId,
            CabinCode = cabinCode,
            FreeBagsIncluded = freeBagsIncluded,
            MaxWeightKgPerBag = maxWeightKgPerBag,
            IsActive = isActive,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
    }
}

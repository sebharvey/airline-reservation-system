namespace ReservationSystem.Microservices.Ancillary.Domain.Entities.Product;

/// <summary>
/// A grouping of ancillary products (e.g. "Duty Free", "Catering").
/// </summary>
public sealed class ProductGroup
{
    public Guid ProductGroupId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private ProductGroup() { }

    public static ProductGroup Create(string name) =>
        new()
        {
            ProductGroupId = Guid.NewGuid(),
            Name = name,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

    public static ProductGroup Reconstitute(
        Guid productGroupId, string name, bool isActive,
        DateTime createdAt, DateTime updatedAt) =>
        new()
        {
            ProductGroupId = productGroupId,
            Name = name,
            IsActive = isActive,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
}

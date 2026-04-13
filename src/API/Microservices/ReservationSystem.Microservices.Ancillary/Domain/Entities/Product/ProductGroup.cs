namespace ReservationSystem.Microservices.Ancillary.Domain.Entities.Product;

/// <summary>
/// A grouping of ancillary products (e.g. "Duty Free", "Catering").
/// </summary>
public sealed class ProductGroup
{
    public Guid ProductGroupId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private ProductGroup() { }

    public static ProductGroup Create(string name, int sortOrder = 0) =>
        new()
        {
            ProductGroupId = Guid.NewGuid(),
            Name = name,
            SortOrder = sortOrder,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

    public static ProductGroup Reconstitute(
        Guid productGroupId, string name, int sortOrder, bool isActive,
        DateTime createdAt, DateTime updatedAt) =>
        new()
        {
            ProductGroupId = productGroupId,
            Name = name,
            SortOrder = sortOrder,
            IsActive = isActive,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
}

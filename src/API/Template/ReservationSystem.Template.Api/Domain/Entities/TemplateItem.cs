using ReservationSystem.Template.Api.Domain.ValueObjects;

namespace ReservationSystem.Template.Api.Domain.Entities;

/// <summary>
/// Core domain entity. Contains business state and enforces invariants.
/// Has no dependency on infrastructure, persistence, or serialisation concerns.
/// </summary>
public sealed class TemplateItem
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Status { get; private set; } = string.Empty;
    public TemplateItemMetadata Metadata { get; private set; } = TemplateItemMetadata.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private TemplateItem() { }

    /// <summary>
    /// Factory method for creating a brand-new item. Assigns a new Id and timestamps.
    /// </summary>
    public static TemplateItem Create(string name, TemplateItemMetadata? metadata = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new TemplateItem
        {
            Id = Guid.NewGuid(),
            Name = name,
            Status = TemplateItemStatus.Active,
            Metadata = metadata ?? TemplateItemMetadata.Empty,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Factory method for reconstituting an entity from a persistence store.
    /// Does not assign a new Id or reset timestamps.
    /// </summary>
    public static TemplateItem Reconstitute(
        Guid id,
        string name,
        string status,
        TemplateItemMetadata metadata,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        return new TemplateItem
        {
            Id = id,
            Name = name,
            Status = status,
            Metadata = metadata,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
    }

    public void UpdateName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Deactivate()
    {
        Status = TemplateItemStatus.Inactive;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}

/// <summary>
/// Allowed status values for a TemplateItem. Kept adjacent to the entity to
/// avoid magic strings across the codebase.
/// </summary>
public static class TemplateItemStatus
{
    public const string Active = "active";
    public const string Inactive = "inactive";
}

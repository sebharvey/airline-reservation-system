namespace ReservationSystem.Template.Api.Domain.ValueObjects;

/// <summary>
/// Value object representing additional structured metadata for a TemplateItem.
/// This data is stored as a JSON column in the database — the serialisation concern
/// lives in Infrastructure; the domain only knows the typed representation here.
/// </summary>
public sealed class TemplateItemMetadata
{
    public IReadOnlyList<string> Tags { get; }
    public string Priority { get; }
    public IReadOnlyDictionary<string, string> Properties { get; }

    public TemplateItemMetadata(
        IReadOnlyList<string>? tags,
        string? priority,
        IReadOnlyDictionary<string, string>? properties)
    {
        Tags = tags ?? [];
        Priority = priority ?? TemplateItemPriority.Normal;
        Properties = properties ?? new Dictionary<string, string>();
    }

    public static TemplateItemMetadata Empty =>
        new([], TemplateItemPriority.Normal, new Dictionary<string, string>());

    /// <summary>Value equality — two instances are equal when their content is equal.</summary>
    public override bool Equals(object? obj)
    {
        if (obj is not TemplateItemMetadata other) return false;

        return Priority == other.Priority
            && Tags.SequenceEqual(other.Tags)
            && Properties.Count == other.Properties.Count
            && Properties.All(kv => other.Properties.TryGetValue(kv.Key, out var v) && v == kv.Value);
    }

    public override int GetHashCode() => HashCode.Combine(Priority, Tags.Count, Properties.Count);
}

public static class TemplateItemPriority
{
    public const string Low = "low";
    public const string Normal = "normal";
    public const string High = "high";
}

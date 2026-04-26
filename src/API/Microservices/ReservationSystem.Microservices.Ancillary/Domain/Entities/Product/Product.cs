namespace ReservationSystem.Microservices.Ancillary.Domain.Entities.Product;

/// <summary>
/// An ancillary product that can be offered to passengers (e.g. duty-free item, paid meal).
/// A product may have multiple <see cref="ProductPrice"/> entries, one per currency.
/// </summary>
public sealed class Product
{
    private const string AllChannelsJson = """["WEB","APP","NDC","GDS","KIOSK","CC","AIRPORT"]""";

    public Guid ProductId { get; private set; }
    public Guid ProductGroupId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public bool IsSegmentSpecific { get; private set; }
    public string? SsrCode { get; private set; }
    public string? ImageBase64 { get; private set; }
    public string AvailableChannels { get; private set; } = AllChannelsJson;
    public string? AvailabilityRules { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    // Navigation — populated by EF via Include
    public List<ProductPrice> Prices { get; private set; } = [];

    private Product() { }

    public static Product Create(
        Guid productGroupId,
        string name,
        string description,
        bool isSegmentSpecific,
        string? ssrCode,
        string? imageBase64,
        string availableChannels = AllChannelsJson,
        string? availabilityRules = null) =>
        new()
        {
            ProductId = Guid.NewGuid(),
            ProductGroupId = productGroupId,
            Name = name,
            Description = description,
            IsSegmentSpecific = isSegmentSpecific,
            SsrCode = string.IsNullOrWhiteSpace(ssrCode) ? null : ssrCode.ToUpperInvariant(),
            ImageBase64 = imageBase64,
            AvailableChannels = string.IsNullOrWhiteSpace(availableChannels) ? AllChannelsJson : availableChannels,
            AvailabilityRules = string.IsNullOrWhiteSpace(availabilityRules) ? null : availabilityRules,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

    public static Product Reconstitute(
        Guid productId, Guid productGroupId, string name, string description,
        bool isSegmentSpecific, string? ssrCode, string? imageBase64,
        string availableChannels, string? availabilityRules, bool isActive,
        DateTime createdAt, DateTime updatedAt) =>
        new()
        {
            ProductId = productId,
            ProductGroupId = productGroupId,
            Name = name,
            Description = description,
            IsSegmentSpecific = isSegmentSpecific,
            SsrCode = ssrCode,
            ImageBase64 = imageBase64,
            AvailableChannels = string.IsNullOrWhiteSpace(availableChannels) ? AllChannelsJson : availableChannels,
            AvailabilityRules = string.IsNullOrWhiteSpace(availabilityRules) ? null : availabilityRules,
            IsActive = isActive,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
}

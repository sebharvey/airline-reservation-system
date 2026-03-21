namespace ReservationSystem.Microservices.Delivery.Domain.Entities;

/// <summary>
/// Core domain entity representing a delivery manifest.
/// A manifest groups tickets and documents associated with a booking.
/// </summary>
public sealed class Manifest
{
    public Guid ManifestId { get; private set; }
    public string BookingReference { get; private set; } = string.Empty;
    public Guid OrderId { get; private set; }
    public string ManifestStatus { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public string ManifestData { get; private set; } = string.Empty;

    private Manifest() { }

    public static Manifest Create(
        string bookingReference,
        Guid orderId,
        string manifestData = "{}")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookingReference);

        return new Manifest
        {
            ManifestId = Guid.NewGuid(),
            BookingReference = bookingReference,
            OrderId = orderId,
            ManifestStatus = ManifestStatusValues.Pending,
            ManifestData = manifestData,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    public static Manifest Reconstitute(
        Guid manifestId,
        string bookingReference,
        Guid orderId,
        string manifestStatus,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        string manifestData)
    {
        return new Manifest
        {
            ManifestId = manifestId,
            BookingReference = bookingReference,
            OrderId = orderId,
            ManifestStatus = manifestStatus,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            ManifestData = manifestData
        };
    }

    public void UpdateData(string manifestData)
    {
        ManifestData = manifestData;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkIssued()
    {
        ManifestStatus = ManifestStatusValues.Issued;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}

public static class ManifestStatusValues
{
    public const string Pending = "pending";
    public const string Issued = "issued";
    public const string Cancelled = "cancelled";
}

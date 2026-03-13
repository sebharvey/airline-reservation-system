using ReservationSystem.Microservices.Offer.Domain.ValueObjects;

namespace ReservationSystem.Microservices.Offer.Domain.Entities;

/// <summary>
/// Core domain entity representing a priced flight offer.
/// Contains business state and enforces invariants.
/// Has no dependency on infrastructure, persistence, or serialisation concerns.
/// </summary>
public sealed class Offer
{
    public Guid Id { get; private set; }
    public string FlightNumber { get; private set; } = string.Empty;
    public string Origin { get; private set; } = string.Empty;
    public string Destination { get; private set; } = string.Empty;
    public DateTimeOffset DepartureAt { get; private set; }
    public string FareClass { get; private set; } = string.Empty;
    public decimal TotalPrice { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public string Status { get; private set; } = string.Empty;
    public OfferMetadata Metadata { get; private set; } = OfferMetadata.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private Offer() { }

    /// <summary>
    /// Factory method for creating a brand-new offer. Assigns a new Id and timestamps.
    /// </summary>
    public static Offer Create(
        string flightNumber,
        string origin,
        string destination,
        DateTimeOffset departureAt,
        string fareClass,
        decimal totalPrice,
        string currency,
        OfferMetadata? metadata = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(flightNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(origin);
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);
        ArgumentException.ThrowIfNullOrWhiteSpace(fareClass);
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);

        return new Offer
        {
            Id = Guid.NewGuid(),
            FlightNumber = flightNumber,
            Origin = origin,
            Destination = destination,
            DepartureAt = departureAt,
            FareClass = fareClass,
            TotalPrice = totalPrice,
            Currency = currency,
            Status = OfferStatus.Available,
            Metadata = metadata ?? OfferMetadata.Empty,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Factory method for reconstituting an entity from a persistence store.
    /// Does not assign a new Id or reset timestamps.
    /// </summary>
    public static Offer Reconstitute(
        Guid id,
        string flightNumber,
        string origin,
        string destination,
        DateTimeOffset departureAt,
        string fareClass,
        decimal totalPrice,
        string currency,
        string status,
        OfferMetadata metadata,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        return new Offer
        {
            Id = id,
            FlightNumber = flightNumber,
            Origin = origin,
            Destination = destination,
            DepartureAt = departureAt,
            FareClass = fareClass,
            TotalPrice = totalPrice,
            Currency = currency,
            Status = status,
            Metadata = metadata,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
    }

    public void Expire()
    {
        Status = OfferStatus.Expired;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkSold()
    {
        Status = OfferStatus.Sold;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}

/// <summary>
/// Allowed status values for an Offer. Kept adjacent to the entity to
/// avoid magic strings across the codebase.
/// </summary>
public static class OfferStatus
{
    public const string Available = "available";
    public const string Sold = "sold";
    public const string Expired = "expired";
}

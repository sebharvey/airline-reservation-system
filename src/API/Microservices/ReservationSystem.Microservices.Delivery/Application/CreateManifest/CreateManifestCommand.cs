namespace ReservationSystem.Microservices.Delivery.Application.CreateManifest;

/// <summary>
/// Command carrying the data needed to create a new delivery manifest.
/// </summary>
public sealed record CreateManifestCommand(
    string BookingReference,
    Guid OrderId,
    string ManifestData);

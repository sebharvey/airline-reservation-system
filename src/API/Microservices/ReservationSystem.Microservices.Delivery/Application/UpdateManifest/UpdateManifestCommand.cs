namespace ReservationSystem.Microservices.Delivery.Application.UpdateManifest;

/// <summary>
/// Command to update the data payload of an existing manifest.
/// </summary>
public sealed record UpdateManifestCommand(Guid ManifestId, string ManifestData);

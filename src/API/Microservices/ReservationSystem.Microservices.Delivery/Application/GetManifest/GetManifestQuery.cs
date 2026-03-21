namespace ReservationSystem.Microservices.Delivery.Application.GetManifest;

/// <summary>
/// Query to retrieve a manifest by its unique identifier.
/// </summary>
public sealed record GetManifestQuery(Guid ManifestId);

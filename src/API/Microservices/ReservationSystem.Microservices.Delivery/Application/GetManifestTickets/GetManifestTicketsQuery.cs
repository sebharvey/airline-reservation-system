namespace ReservationSystem.Microservices.Delivery.Application.GetManifestTickets;

/// <summary>
/// Query to retrieve all tickets associated with a manifest.
/// </summary>
public sealed record GetManifestTicketsQuery(Guid ManifestId);

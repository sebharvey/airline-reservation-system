namespace ReservationSystem.Microservices.Delivery.Application.UpdateManifestSsrs;

public sealed record UpdateManifestSsrsCommand(string BookingReference, IReadOnlyDictionary<string, string?> SsrsByETicket);

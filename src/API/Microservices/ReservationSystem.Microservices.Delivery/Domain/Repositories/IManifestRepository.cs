using ReservationSystem.Microservices.Delivery.Domain.Entities;

namespace ReservationSystem.Microservices.Delivery.Domain.Repositories;

public interface IManifestRepository
{
    /// <summary>
    /// Inserts a manifest entry. Returns <c>true</c> if inserted,
    /// <c>false</c> if a duplicate entry already exists for this inventory/passenger.
    /// </summary>
    Task<bool> CreateAsync(Manifest manifest, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Manifest>> GetByFlightAsync(string flightNumber, DateOnly departureDate, CancellationToken cancellationToken = default);

    Task<int> DeleteByBookingAndFlightAsync(string bookingReference, string flightNumber, DateOnly departureDate, CancellationToken cancellationToken = default);
}

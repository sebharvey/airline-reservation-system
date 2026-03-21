using ReservationSystem.Microservices.Delivery.Domain.Entities;

namespace ReservationSystem.Microservices.Delivery.Domain.Repositories;

public interface IManifestRepository
{
    Task<Manifest?> GetByIdAsync(Guid manifestId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Manifest>> GetByFlightAsync(string flightNumber, DateTime departureDate, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Manifest>> GetByBookingAndFlightAsync(string bookingReference, string flightNumber, DateTime departureDate, CancellationToken cancellationToken = default);

    Task<Manifest?> GetByInventoryAndPassengerAsync(Guid inventoryId, string passengerId, CancellationToken cancellationToken = default);

    Task CreateAsync(Manifest manifest, CancellationToken cancellationToken = default);

    Task UpdateAsync(Manifest manifest, CancellationToken cancellationToken = default);

    Task<int> DeleteByBookingAndFlightAsync(string bookingReference, string flightNumber, DateTime departureDate, CancellationToken cancellationToken = default);
}

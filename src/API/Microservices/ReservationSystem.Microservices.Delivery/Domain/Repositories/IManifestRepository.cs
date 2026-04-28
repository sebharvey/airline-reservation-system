using ReservationSystem.Microservices.Delivery.Domain.Entities;

namespace ReservationSystem.Microservices.Delivery.Domain.Repositories;

public sealed record ManifestPassengerRebook(Guid TicketId, string ETicketNumber);

public interface IManifestRepository
{
    /// <summary>
    /// Inserts a manifest entry. Returns <c>true</c> if inserted,
    /// <c>false</c> if a duplicate entry already exists for this inventory/passenger.
    /// </summary>
    Task<bool> CreateAsync(Manifest manifest, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Manifest>> GetByFlightAsync(string flightNumber, DateOnly departureDate, CancellationToken cancellationToken = default);

    Task<int> DeleteByBookingAndFlightAsync(string bookingReference, string flightNumber, DateOnly departureDate, CancellationToken cancellationToken = default);

    Task<bool> CheckInByETicketAndOriginAsync(string eTicketNumber, string origin, DateTime checkedInAt, CancellationToken cancellationToken = default);

    Task<bool> UpdateSeatByETicketAsync(string eTicketNumber, string? newSeatNumber, CancellationToken cancellationToken = default);

    Task<int> RebookByBookingAndFlightAsync(
        string bookingReference,
        string fromFlightNumber,
        DateOnly fromDepartureDate,
        Guid toInventoryId,
        string toFlightNumber,
        string toOrigin,
        string toDestination,
        DateOnly toDepartureDate,
        TimeOnly toDepartureTime,
        TimeOnly toArrivalTime,
        string toCabinCode,
        IReadOnlyDictionary<string, ManifestPassengerRebook> passengerRebooks,
        CancellationToken cancellationToken = default);
}

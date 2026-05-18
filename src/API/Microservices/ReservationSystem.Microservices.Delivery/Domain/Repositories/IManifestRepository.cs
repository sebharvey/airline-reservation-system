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

    Task<bool> CheckInByETicketAndOriginAsync(string eTicketNumber, string origin, DateTime checkedInAt, string? baggageJson = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the seat on the manifest entry matching <paramref name="eTicketNumber"/> and
    /// <paramref name="inventoryId"/>. Returns the updated entry (with Origin populated) or
    /// <c>null</c> if no matching entry was found.
    /// </summary>
    Task<Manifest?> UpdateSeatByETicketAsync(string eTicketNumber, Guid inventoryId, string? newSeatNumber, CancellationToken cancellationToken = default);

    Task<bool> UpdateSeatByETicketAndOriginAsync(string eTicketNumber, string origin, string? newSeatNumber, CancellationToken cancellationToken = default);

    Task<int> UpdateSsrCodesByBookingAsync(string bookingReference, IReadOnlyDictionary<string, string?> ssrsByETicket, CancellationToken cancellationToken = default);

    Task<int> RebookByBookingAndFlightAsync(
        string bookingReference,
        string fromFlightNumber,
        DateOnly fromDepartureDate,
        Guid toInventoryId,
        int toSegmentId,
        string toFlightNumber,
        string toOrigin,
        string toDestination,
        DateOnly toDepartureDate,
        TimeOnly toDepartureTime,
        TimeOnly toArrivalTime,
        string toCabinCode,
        IReadOnlyDictionary<int, ManifestPassengerRebook> passengerRebooks,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all non-null seat numbers already assigned to passengers on a given flight,
    /// sourced from the manifest (the authoritative record of seat assignments across all bookings).
    /// Used by the seat allocator to avoid double-assigning a seat at check-in.
    /// </summary>
    Task<IReadOnlyList<string>> GetAssignedSeatsByFlightAsync(string flightNumber, string origin, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates departure/arrival times and sets FlightStatus = 'Delayed' on all manifest rows
    /// for the given flight. Returns the number of rows updated.
    /// </summary>
    Task<int> UpdateFlightTimesAsync(string flightNumber, DateOnly departureDate, TimeOnly newDepartureTime, TimeOnly newArrivalTime, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all manifest entries whose <c>DepartureDate</c> is more than 48 hours in the past.
    /// Returns the number of rows deleted.
    /// </summary>
    Task<int> DeleteExpiredManifestItemsAsync(CancellationToken cancellationToken = default);
}

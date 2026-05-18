using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Delivery.Domain.Entities;
using ReservationSystem.Microservices.Delivery.Domain.Repositories;

namespace ReservationSystem.Microservices.Delivery.Infrastructure.Persistence;

public sealed class EfManifestRepository : IManifestRepository
{
    private readonly DeliveryDbContext _context;
    private readonly ILogger<EfManifestRepository> _logger;

    public EfManifestRepository(DeliveryDbContext context, ILogger<EfManifestRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<bool> CreateAsync(Manifest manifest, CancellationToken cancellationToken = default)
    {
        _context.Manifests.Add(manifest);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogDebug(
                "Inserted manifest entry {ManifestId} for {PassengerId} on {FlightNumber}",
                manifest.ManifestId, manifest.PassengerId, manifest.FlightNumber);
            return true;
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 })
        {
            _context.Entry(manifest).State = EntityState.Detached;
            _logger.LogDebug(
                "Manifest entry for {PassengerId} on inventory {InventoryId} already exists — skipped",
                manifest.PassengerId, manifest.InventoryId);
            return false;
        }
    }

    public async Task<IReadOnlyList<Manifest>> GetByFlightAsync(
        string flightNumber,
        DateOnly departureDate,
        CancellationToken cancellationToken = default)
    {
        return await _context.Manifests
            .Where(m => m.FlightNumber == flightNumber && m.DepartureDate == departureDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> RebookByBookingAndFlightAsync(
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
        CancellationToken cancellationToken = default)
    {
        var entries = await _context.Manifests
            .Where(m => m.BookingReference == bookingReference
                     && m.FlightNumber == fromFlightNumber
                     && m.DepartureDate == fromDepartureDate)
            .ToListAsync(cancellationToken);

        if (entries.Count == 0)
            return 0;

        var updated = 0;
        foreach (var entry in entries)
        {
            if (!passengerRebooks.TryGetValue(entry.PassengerId, out var rebook))
            {
                _logger.LogWarning(
                    "No rebook info for passenger {PassengerId} in booking {BookingRef} — skipping manifest update",
                    entry.PassengerId, bookingReference);
                continue;
            }

            entry.Rebook(
                toInventoryId, rebook.TicketId,
                toFlightNumber, toOrigin, toDestination,
                toDepartureDate, toDepartureTime, toArrivalTime,
                toCabinCode, rebook.ETicketNumber, toSegmentId);
            updated++;
        }

        if (updated > 0)
            await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug(
            "Rebooked {Count} manifest entries for booking {BookingRef}: {FromFlight} → {ToFlight}",
            updated, bookingReference, fromFlightNumber, toFlightNumber);

        return updated;
    }

    public async Task<bool> CheckInByETicketAndOriginAsync(
        string eTicketNumber, string origin, DateTime checkedInAt, string? baggageJson = null, CancellationToken cancellationToken = default)
    {
        var entry = await _context.Manifests
            .FirstOrDefaultAsync(
                m => m.ETicketNumber == eTicketNumber && m.Origin == origin,
                cancellationToken);

        if (entry is null)
        {
            _logger.LogWarning(
                "Manifest entry not found for e-ticket {ETicketNumber} / origin {Origin} — check-in flag not set",
                eTicketNumber, origin);
            return false;
        }

        entry.CheckIn(checkedInAt, baggageJson);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug(
            "Marked manifest entry for e-ticket {ETicketNumber} / origin {Origin} as checked in at {CheckedInAt}",
            eTicketNumber, origin, checkedInAt);

        return true;
    }

    public async Task<Manifest?> UpdateSeatByETicketAsync(
        string eTicketNumber, Guid inventoryId, string? newSeatNumber, CancellationToken cancellationToken = default)
    {
        var entry = await _context.Manifests
            .FirstOrDefaultAsync(m => m.ETicketNumber == eTicketNumber && m.InventoryId == inventoryId, cancellationToken);

        if (entry is null)
            return null;

        entry.UpdateSeat(newSeatNumber);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug(
            "Updated seat to '{SeatNumber}' on manifest entry for e-ticket {ETicketNumber}",
            newSeatNumber ?? "(none)", eTicketNumber);

        return entry;
    }

    public async Task<bool> UpdateSeatByETicketAndOriginAsync(
        string eTicketNumber, string origin, string? newSeatNumber, CancellationToken cancellationToken = default)
    {
        var entry = await _context.Manifests
            .FirstOrDefaultAsync(m => m.ETicketNumber == eTicketNumber && m.Origin == origin, cancellationToken);

        if (entry is null)
            return false;

        entry.UpdateSeat(newSeatNumber);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug(
            "Updated seat to '{SeatNumber}' on manifest entry for e-ticket {ETicketNumber} / origin {Origin}",
            newSeatNumber ?? "(none)", eTicketNumber, origin);

        return true;
    }

    public async Task<int> UpdateSsrCodesByBookingAsync(
        string bookingReference,
        IReadOnlyDictionary<string, string?> ssrsByETicket,
        CancellationToken cancellationToken = default)
    {
        var eTicketNumbers = ssrsByETicket.Keys.ToList();

        var entries = await _context.Manifests
            .Where(m => m.BookingReference == bookingReference
                     && eTicketNumbers.Contains(m.ETicketNumber))
            .ToListAsync(cancellationToken);

        if (entries.Count == 0)
            return 0;

        foreach (var entry in entries)
        {
            if (ssrsByETicket.TryGetValue(entry.ETicketNumber, out var ssrCodesJson))
                entry.UpdateSsrCodes(ssrCodesJson);
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug(
            "Updated SSR codes on {Count} manifest entries for booking {BookingRef}",
            entries.Count, bookingReference);

        return entries.Count;
    }

    public async Task<IReadOnlyList<string>> GetAssignedSeatsByFlightAsync(
        string flightNumber, string origin, CancellationToken cancellationToken = default)
    {
        return await _context.Manifests
            .Where(m => m.FlightNumber == flightNumber
                     && m.Origin      == origin
                     && m.SeatNumber  != null
                     && m.SeatNumber  != "")
            .Select(m => m.SeatNumber!)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    public async Task<int> DeleteExpiredManifestItemsAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-48));

        var expired = await _context.Manifests
            .Where(m => m.DepartureDate < cutoff)
            .ToListAsync(cancellationToken);

        if (expired.Count == 0)
            return 0;

        _context.Manifests.RemoveRange(expired);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Deleted {Count} expired manifest entries with DepartureDate before {Cutoff}", expired.Count, cutoff);

        return expired.Count;
    }

    public async Task<int> UpdateFlightTimesAsync(
        string flightNumber,
        DateOnly departureDate,
        TimeOnly newDepartureTime,
        TimeOnly newArrivalTime,
        CancellationToken cancellationToken = default)
    {
        var entries = await _context.Manifests
            .Where(m => m.FlightNumber == flightNumber && m.DepartureDate == departureDate)
            .ToListAsync(cancellationToken);

        if (entries.Count == 0)
            return 0;

        foreach (var entry in entries)
            entry.ApplyDelay(newDepartureTime, newArrivalTime);

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug(
            "Updated flight times on {Count} manifest entries for {FlightNumber}/{DepartureDate} — status set to Delayed",
            entries.Count, flightNumber, departureDate);

        return entries.Count;
    }

    public async Task<int> DeleteByBookingAndFlightAsync(
        string bookingReference,
        string flightNumber,
        DateOnly departureDate,
        CancellationToken cancellationToken = default)
    {
        var entries = await _context.Manifests
            .Where(m => m.BookingReference == bookingReference
                     && m.FlightNumber == flightNumber
                     && m.DepartureDate == departureDate)
            .ToListAsync(cancellationToken);

        if (entries.Count == 0)
            return 0;

        _context.Manifests.RemoveRange(entries);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug(
            "Deleted {Count} manifest entries for booking {BookingRef} on {FlightNumber}/{DepartureDate}",
            entries.Count, bookingReference, flightNumber, departureDate);

        return entries.Count;
    }
}

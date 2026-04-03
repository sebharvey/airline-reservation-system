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

    public async Task<Manifest?> GetByIdAsync(Guid manifestId, CancellationToken cancellationToken = default)
    {
        return await _context.Manifests
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.ManifestId == manifestId, cancellationToken);
    }

    public async Task<IReadOnlyList<Manifest>> GetByFlightAsync(string flightNumber, DateTime departureDate, CancellationToken cancellationToken = default)
    {
        var manifests = await _context.Manifests
            .AsNoTracking()
            .Where(m => m.FlightNumber == flightNumber && m.DepartureDate == departureDate)
            .OrderBy(m => m.SeatNumber)
            .ToListAsync(cancellationToken);
        return manifests.AsReadOnly();
    }

    public async Task<IReadOnlyList<Manifest>> GetByBookingAsync(string bookingReference, CancellationToken cancellationToken = default)
    {
        var manifests = await _context.Manifests
            .AsNoTracking()
            .Where(m => m.BookingReference == bookingReference && m.CheckedIn)
            .OrderBy(m => m.PassengerId).ThenBy(m => m.DepartureDate)
            .ToListAsync(cancellationToken);
        return manifests.AsReadOnly();
    }

    public async Task<IReadOnlyList<Manifest>> GetByBookingAndFlightAsync(string bookingReference, string flightNumber, DateTime departureDate, CancellationToken cancellationToken = default)
    {
        var manifests = await _context.Manifests
            .AsNoTracking()
            .Where(m => m.BookingReference == bookingReference
                     && m.FlightNumber == flightNumber
                     && m.DepartureDate == departureDate)
            .ToListAsync(cancellationToken);
        return manifests.AsReadOnly();
    }

    public async Task<Manifest?> GetByInventoryAndPassengerAsync(Guid inventoryId, string passengerId, CancellationToken cancellationToken = default)
    {
        return await _context.Manifests
            .FirstOrDefaultAsync(m => m.InventoryId == inventoryId && m.PassengerId == passengerId, cancellationToken);
    }

    public async Task CreateAsync(Manifest manifest, CancellationToken cancellationToken = default)
    {
        _context.Manifests.Add(manifest);
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogDebug("Inserted Manifest {ManifestId} for {BookingReference}", manifest.ManifestId, manifest.BookingReference);
    }

    public async Task UpdateAsync(Manifest manifest, CancellationToken cancellationToken = default)
    {
        _context.Manifests.Update(manifest);
        var rowsAffected = await _context.SaveChangesAsync(cancellationToken);
        if (rowsAffected == 0)
            _logger.LogWarning("UpdateAsync found no row for Manifest {ManifestId}", manifest.ManifestId);
    }

    public async Task<int> DeleteByBookingAndFlightAsync(string bookingReference, string flightNumber, DateTime departureDate, CancellationToken cancellationToken = default)
    {
        var manifests = await _context.Manifests
            .Where(m => m.BookingReference == bookingReference
                     && m.FlightNumber == flightNumber
                     && m.DepartureDate == departureDate)
            .ToListAsync(cancellationToken);

        if (manifests.Count == 0) return 0;

        _context.Manifests.RemoveRange(manifests);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Deleted {Count} manifest entries for {BookingReference}/{FlightNumber}/{Date}",
            manifests.Count, bookingReference, flightNumber, departureDate);

        return manifests.Count;
    }

    public async Task<IReadOnlyList<Manifest>> GetByETicketNumberAsync(string eTicketNumber, CancellationToken cancellationToken = default)
    {
        var manifests = await _context.Manifests
            .Where(m => m.ETicketNumber == eTicketNumber)
            .OrderBy(m => m.DepartureDate)
            .ToListAsync(cancellationToken);
        return manifests.AsReadOnly();
    }
}

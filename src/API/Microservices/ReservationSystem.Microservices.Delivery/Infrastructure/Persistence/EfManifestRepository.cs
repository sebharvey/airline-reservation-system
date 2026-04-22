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

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Delivery.Domain.Entities;
using ReservationSystem.Microservices.Delivery.Domain.Exceptions;
using ReservationSystem.Microservices.Delivery.Domain.Repositories;

namespace ReservationSystem.Microservices.Delivery.Infrastructure.Persistence;

public sealed class EfTicketRepository : ITicketRepository
{
    private readonly DeliveryDbContext _context;
    private readonly ILogger<EfTicketRepository> _logger;

    public EfTicketRepository(DeliveryDbContext context, ILogger<EfTicketRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Ticket?> GetByIdAsync(Guid ticketId, CancellationToken cancellationToken = default)
    {
        return await _context.Tickets
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TicketId == ticketId, cancellationToken);
    }

    public async Task<Ticket?> GetByETicketNumberAsync(string eTicketNumber, CancellationToken cancellationToken = default)
    {
        var ticketNumber = ParseTicketNumber(eTicketNumber);
        if (ticketNumber is null) return null;

        return await _context.Tickets
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TicketNumber == ticketNumber.Value, cancellationToken);
    }

    public async Task<IReadOnlyList<Ticket>> GetByBookingReferenceAsync(string bookingReference, CancellationToken cancellationToken = default)
    {
        var tickets = await _context.Tickets
            .AsNoTracking()
            .Where(t => t.BookingReference == bookingReference)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync(cancellationToken);
        return tickets.AsReadOnly();
    }

    public async Task CreateAsync(Ticket ticket, CancellationToken cancellationToken = default)
    {
        _context.Tickets.Add(ticket);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 })
        {
            _context.Entry(ticket).State = EntityState.Detached;
            throw new TicketNumberConflictException(ticket.TicketNumber);
        }
        _logger.LogDebug("Inserted Ticket {TicketId} ({TicketNumber})", ticket.TicketId, ticket.TicketNumber);
    }

    public async Task UpdateAsync(Ticket ticket, CancellationToken cancellationToken = default)
    {
        _context.Tickets.Update(ticket);
        var rowsAffected = await _context.SaveChangesAsync(cancellationToken);
        if (rowsAffected == 0)
            _logger.LogWarning("UpdateAsync found no row for Ticket {TicketId}", ticket.TicketId);
    }

    public async Task<IReadOnlyList<string>> GetAssignedSeatsForFlightAsync(
        string flightNumber, string origin, CancellationToken cancellationToken = default)
    {
        var seats = await _context.Database
            .SqlQuery<string>($"""
                SELECT c.Seat
                FROM [delivery].[Ticket] t
                CROSS APPLY OPENJSON(t.TicketData, '$.coupons') WITH (
                    FlightNumber NVARCHAR(10) '$.marketing.flightNumber',
                    Origin       NVARCHAR(3)  '$.origin',
                    Seat         NVARCHAR(10) '$.seat'
                ) c
                WHERE t.IsVoided = 0
                  AND c.FlightNumber = {flightNumber}
                  AND c.Origin       = {origin}
                  AND c.Seat IS NOT NULL
                  AND c.Seat <> ''
                """)
            .ToListAsync(cancellationToken);

        return seats.AsReadOnly();
    }

    private static long? ParseTicketNumber(string eTicketNumber)
    {
        var dashIndex = eTicketNumber.IndexOf('-');
        if (dashIndex < 0) return null;
        return long.TryParse(eTicketNumber[(dashIndex + 1)..], out var num) ? num : null;
    }
}

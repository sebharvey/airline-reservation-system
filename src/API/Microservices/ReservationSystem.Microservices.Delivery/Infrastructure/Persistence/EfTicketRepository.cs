using System.Text.Json.Nodes;
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
        var candidates = await _context.Tickets
            .FromSqlInterpolated($"SELECT * FROM Tickets WHERE IsVoided = 0 AND CAST(TicketData AS nvarchar(max)) LIKE '%' + {flightNumber} + '%'")
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var seats = new List<string>();
        foreach (var ticket in candidates)
        {
            try
            {
                var root = JsonNode.Parse(ticket.TicketData)?.AsObject();
                var coupons = root?["coupons"]?.AsArray();
                if (coupons is null) continue;

                foreach (var node in coupons)
                {
                    if (node is not JsonObject coupon) continue;
                    var couponOrigin = coupon["origin"]?.GetValue<string>() ?? "";
                    var couponFlight = coupon["marketing"]?["flightNumber"]?.GetValue<string>() ?? "";
                    var seat = coupon["seat"]?.GetValue<string>();

                    if (string.Equals(couponOrigin, origin, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(couponFlight, flightNumber, StringComparison.OrdinalIgnoreCase) &&
                        !string.IsNullOrWhiteSpace(seat))
                    {
                        seats.Add(seat!);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse TicketData for seat allocation on ticket {TicketId}", ticket.TicketId);
            }
        }

        return seats.AsReadOnly();
    }

    private static long? ParseTicketNumber(string eTicketNumber)
    {
        var dashIndex = eTicketNumber.IndexOf('-');
        if (dashIndex < 0) return null;
        return long.TryParse(eTicketNumber[(dashIndex + 1)..], out var num) ? num : null;
    }
}

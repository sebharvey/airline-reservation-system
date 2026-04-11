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
        return await _context.Tickets
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.ETicketNumber == eTicketNumber, cancellationToken);
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
            throw new TicketNumberConflictException(ticket.ETicketNumber);
        }
        _logger.LogDebug("Inserted Ticket {TicketId} ({ETicketNumber})", ticket.TicketId, ticket.ETicketNumber);
    }

    public async Task UpdateAsync(Ticket ticket, CancellationToken cancellationToken = default)
    {
        _context.Tickets.Update(ticket);
        var rowsAffected = await _context.SaveChangesAsync(cancellationToken);
        if (rowsAffected == 0)
            _logger.LogWarning("UpdateAsync found no row for Ticket {TicketId}", ticket.TicketId);
    }

    public async Task<long> GetMaxTicketSequenceAsync(CancellationToken cancellationToken = default)
    {
        // ETicketNumber format: "932-XXXXXXXXXX" — extract the 10-digit numeric suffix.
        // Returns 0 when the table is empty so the first ticket gets sequence 1.
        // SqlQueryRaw<T> for primitives requires the column to be aliased as "Value".
        return await _context.Database
            .SqlQueryRaw<long>(
                "SELECT ISNULL(MAX(TRY_CAST(SUBSTRING(ETicketNumber, 5, 10) AS BIGINT)), 0) AS Value " +
                "FROM [delivery].[Ticket]")
            .FirstAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetAssignedSeatsForFlightAsync(
        string flightNumber, string origin, CancellationToken cancellationToken = default)
    {
        // Broad SQL filter: pull tickets whose JSON data mentions this flight number,
        // then refine in memory to match origin and extract assigned seats.
        var candidates = await _context.Tickets
            .AsNoTracking()
            .Where(t => !t.IsVoided && t.TicketData.Contains(flightNumber))
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
}

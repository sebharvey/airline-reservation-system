using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Delivery.Domain.Entities;
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
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogDebug("Inserted Ticket {TicketId} ({ETicketNumber})", ticket.TicketId, ticket.ETicketNumber);
    }

    public async Task UpdateAsync(Ticket ticket, CancellationToken cancellationToken = default)
    {
        _context.Tickets.Update(ticket);
        var rowsAffected = await _context.SaveChangesAsync(cancellationToken);
        if (rowsAffected == 0)
            _logger.LogWarning("UpdateAsync found no row for Ticket {TicketId}", ticket.TicketId);
    }

    public async Task<int> GetTicketCountAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Tickets.CountAsync(cancellationToken);
    }
}

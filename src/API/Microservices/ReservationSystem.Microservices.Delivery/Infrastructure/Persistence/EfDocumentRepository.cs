using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Delivery.Domain.Entities;
using ReservationSystem.Microservices.Delivery.Domain.Repositories;

namespace ReservationSystem.Microservices.Delivery.Infrastructure.Persistence;

public sealed class EfDocumentRepository : IDocumentRepository
{
    private readonly DeliveryDbContext _context;
    private readonly ILogger<EfDocumentRepository> _logger;

    public EfDocumentRepository(DeliveryDbContext context, ILogger<EfDocumentRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Document?> GetByIdAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        return await _context.Documents
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.DocumentId == documentId, cancellationToken);
    }

    public async Task<IReadOnlyList<Document>> GetByBookingReferenceAsync(string bookingReference, CancellationToken cancellationToken = default)
    {
        var documents = await _context.Documents
            .AsNoTracking()
            .Where(d => d.BookingReference == bookingReference)
            .OrderBy(d => d.CreatedAt)
            .ToListAsync(cancellationToken);
        return documents.AsReadOnly();
    }

    public async Task CreateAsync(Document document, CancellationToken cancellationToken = default)
    {
        _context.Documents.Add(document);
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogDebug("Inserted Document {DocumentId} ({DocumentNumber})", document.DocumentId, document.DocumentNumber);
    }

    public async Task<int> GetDocumentCountAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Documents.CountAsync(cancellationToken);
    }
}

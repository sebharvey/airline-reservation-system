using Microsoft.EntityFrameworkCore;
using ReservationSystem.Microservices.Customer.Domain.Entities;
using ReservationSystem.Microservices.Customer.Domain.Repositories;

namespace ReservationSystem.Microservices.Customer.Infrastructure.Persistence;

public sealed class EfCustomerNoteRepository : ICustomerNoteRepository
{
    private readonly CustomerDbContext _db;

    public EfCustomerNoteRepository(CustomerDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<CustomerNote>> GetByCustomerIdAsync(
        Guid customerId, CancellationToken cancellationToken = default)
    {
        return await _db.CustomerNotes
            .Where(n => n.CustomerId == customerId)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<CustomerNote?> GetByIdAsync(Guid noteId, CancellationToken cancellationToken = default)
    {
        return await _db.CustomerNotes
            .FirstOrDefaultAsync(n => n.NoteId == noteId, cancellationToken);
    }

    public async Task AddAsync(CustomerNote note, CancellationToken cancellationToken = default)
    {
        _db.CustomerNotes.Add(note);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(CustomerNote note, CancellationToken cancellationToken = default)
    {
        _db.CustomerNotes.Update(note);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(CustomerNote note, CancellationToken cancellationToken = default)
    {
        _db.CustomerNotes.Remove(note);
        await _db.SaveChangesAsync(cancellationToken);
    }
}

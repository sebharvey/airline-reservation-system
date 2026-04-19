using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Payment.Domain.Entities;
using ReservationSystem.Microservices.Payment.Domain.Repositories;

namespace ReservationSystem.Microservices.Payment.Infrastructure.Persistence;

/// <summary>
/// Entity Framework Core implementation of <see cref="IPaymentRepository"/>.
///
/// Uses <see cref="PaymentDbContext"/> to interact with the payment schema.
/// The DbContext is scoped (one per function invocation) so no manual connection
/// management is required — EF handles connection lifetime internally.
/// </summary>
public sealed class EfPaymentRepository : IPaymentRepository
{
    private readonly PaymentDbContext _dbContext;
    private readonly ILogger<EfPaymentRepository> _logger;

    public EfPaymentRepository(PaymentDbContext dbContext, ILogger<EfPaymentRepository> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<Domain.Entities.Payment?> GetByIdAsync(Guid paymentId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Payments
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PaymentId == paymentId, cancellationToken);
    }

    public async Task CreateAsync(Domain.Entities.Payment payment, CancellationToken cancellationToken = default)
    {
        _dbContext.Payments.Add(payment);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Inserted Payment {PaymentId} into [payment].[Payment]", payment.PaymentId);
    }

    public async Task UpdateAsync(Domain.Entities.Payment payment, CancellationToken cancellationToken = default)
    {
        _dbContext.Payments.Update(payment);
        var rowsAffected = await _dbContext.SaveChangesAsync(cancellationToken);

        if (rowsAffected == 0)
            _logger.LogWarning("UpdateAsync found no row for Payment {PaymentId}", payment.PaymentId);
        else
            _logger.LogDebug("Updated Payment {PaymentId} in [payment].[Payment]", payment.PaymentId);
    }

    public async Task CreateEventAsync(PaymentEvent paymentEvent, CancellationToken cancellationToken = default)
    {
        _dbContext.PaymentEvents.Add(paymentEvent);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Inserted PaymentEvent {PaymentEventId} into [payment].[PaymentEvent]", paymentEvent.PaymentEventId);
    }

    public async Task UpdateEventAsync(PaymentEvent paymentEvent, CancellationToken cancellationToken = default)
    {
        _dbContext.PaymentEvents.Update(paymentEvent);
        var rowsAffected = await _dbContext.SaveChangesAsync(cancellationToken);

        if (rowsAffected == 0)
            _logger.LogWarning("UpdateEventAsync found no row for PaymentEvent {PaymentEventId}", paymentEvent.PaymentEventId);
        else
            _logger.LogDebug("Updated PaymentEvent {PaymentEventId} in [payment].[PaymentEvent]", paymentEvent.PaymentEventId);
    }

    public async Task<PaymentEvent?> GetEventByPaymentIdAsync(Guid paymentId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.PaymentEvents
            .Where(pe => pe.PaymentId == paymentId && pe.EventType == PaymentEventType.Authorised)
            .OrderByDescending(pe => pe.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PaymentEvent>> GetEventsByPaymentIdAsync(Guid paymentId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.PaymentEvents
            .AsNoTracking()
            .Where(pe => pe.PaymentId == paymentId)
            .OrderBy(pe => pe.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<(Domain.Entities.Payment Payment, int EventCount)>> GetByDateWithEventCountAsync(
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        var start = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var end   = date.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

        var rows = await _dbContext.Payments
            .AsNoTracking()
            .Where(p => p.CreatedAt >= start && p.CreatedAt <= end)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new
            {
                Payment    = p,
                EventCount = _dbContext.PaymentEvents.Count(e => e.PaymentId == p.PaymentId)
            })
            .ToListAsync(cancellationToken);

        return rows.Select(r => (r.Payment, r.EventCount)).ToList();
    }
}

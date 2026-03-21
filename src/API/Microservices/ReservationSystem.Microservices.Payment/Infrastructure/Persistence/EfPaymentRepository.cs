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

    public async Task<Payment?> GetByIdAsync(Guid paymentId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Payments
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PaymentId == paymentId, cancellationToken);
    }

    public async Task<Payment?> GetByReferenceAsync(string paymentReference, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Payments
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PaymentReference == paymentReference, cancellationToken);
    }

    public async Task CreateAsync(Payment payment, CancellationToken cancellationToken = default)
    {
        _dbContext.Payments.Add(payment);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Inserted Payment {PaymentId} into [payment].[Payment]", payment.PaymentId);
    }

    public async Task UpdateAsync(Payment payment, CancellationToken cancellationToken = default)
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
}

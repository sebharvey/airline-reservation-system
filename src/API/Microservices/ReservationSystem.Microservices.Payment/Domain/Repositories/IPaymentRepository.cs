using ReservationSystem.Microservices.Payment.Domain.Entities;

namespace ReservationSystem.Microservices.Payment.Domain.Repositories;

/// <summary>
/// Port (interface) for Payment persistence.
/// Defined in Domain so the Application layer can depend on it without
/// taking a dependency on Infrastructure. The EF implementation lives in
/// Infrastructure/Persistence and is registered via DI at startup.
/// </summary>
public interface IPaymentRepository
{
    Task<Entities.Payment?> GetByIdAsync(Guid paymentId, CancellationToken cancellationToken = default);

    Task CreateAsync(Entities.Payment payment, CancellationToken cancellationToken = default);

    Task UpdateAsync(Entities.Payment payment, CancellationToken cancellationToken = default);

    Task CreateEventAsync(PaymentEvent paymentEvent, CancellationToken cancellationToken = default);

    Task UpdateEventAsync(PaymentEvent paymentEvent, CancellationToken cancellationToken = default);

    Task<PaymentEvent?> GetEventByPaymentIdAsync(Guid paymentId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PaymentEvent>> GetEventsByPaymentIdAsync(Guid paymentId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<(Entities.Payment Payment, int EventCount)>> GetByDateWithEventCountAsync(DateOnly date, CancellationToken cancellationToken = default);
}

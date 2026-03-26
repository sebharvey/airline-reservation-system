using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Payment.Domain.Repositories;
using ReservationSystem.Microservices.Payment.Models.Mappers;
using ReservationSystem.Microservices.Payment.Models.Responses;

namespace ReservationSystem.Microservices.Payment.Application.GetPaymentEvents;

/// <summary>
/// Handles the <see cref="GetPaymentEventsQuery"/>.
/// Retrieves all payment event records for a payment, ordered chronologically.
/// Returns null when no payment with the given ID exists.
/// </summary>
public sealed class GetPaymentEventsHandler
{
    private readonly IPaymentRepository _repository;
    private readonly ILogger<GetPaymentEventsHandler> _logger;

    public GetPaymentEventsHandler(IPaymentRepository repository, ILogger<GetPaymentEventsHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<IReadOnlyList<PaymentEventResponse>?> HandleAsync(
        GetPaymentEventsQuery query,
        CancellationToken cancellationToken = default)
    {
        var payment = await _repository.GetByIdAsync(query.PaymentId, cancellationToken);

        if (payment is null)
        {
            _logger.LogWarning("Payment {PaymentId} not found when retrieving events", query.PaymentId);
            return null;
        }

        var events = await _repository.GetEventsByPaymentIdAsync(query.PaymentId, cancellationToken);

        return events.Select(PaymentMapper.ToPaymentEventResponse).ToList();
    }
}

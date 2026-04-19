using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Payment.Domain.Repositories;
using ReservationSystem.Microservices.Payment.Models.Responses;

namespace ReservationSystem.Microservices.Payment.Application.GetPaymentsByDate;

/// <summary>
/// Handles <see cref="GetPaymentsByDateQuery"/>.
/// Returns all payments created on the requested date, each with a pre-computed
/// event count so the list view requires no further round-trips.
/// </summary>
public sealed class GetPaymentsByDateHandler
{
    private readonly IPaymentRepository _repository;
    private readonly ILogger<GetPaymentsByDateHandler> _logger;

    public GetPaymentsByDateHandler(IPaymentRepository repository, ILogger<GetPaymentsByDateHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<IReadOnlyList<PaymentListItemResponse>> HandleAsync(
        GetPaymentsByDateQuery query,
        CancellationToken cancellationToken = default)
    {
        var results = await _repository.GetByDateWithEventCountAsync(query.Date, cancellationToken);

        _logger.LogDebug("Retrieved {Count} payments for {Date}", results.Count, query.Date);

        return results.Select(r => new PaymentListItemResponse
        {
            PaymentId        = r.Payment.PaymentId,
            BookingReference = r.Payment.BookingReference,
            PaymentType      = r.Payment.PaymentType,
            Method           = r.Payment.Method,
            CardType         = r.Payment.CardType,
            CardLast4        = r.Payment.CardLast4,
            CurrencyCode     = r.Payment.CurrencyCode,
            Amount           = r.Payment.Amount,
            AuthorisedAmount = r.Payment.AuthorisedAmount,
            SettledAmount    = r.Payment.SettledAmount,
            Status           = r.Payment.Status,
            AuthorisedAt     = r.Payment.AuthorisedAt,
            SettledAt        = r.Payment.SettledAt,
            Description      = r.Payment.Description,
            CreatedAt        = r.Payment.CreatedAt,
            UpdatedAt        = r.Payment.UpdatedAt,
            EventCount       = r.EventCount
        }).ToList();
    }
}

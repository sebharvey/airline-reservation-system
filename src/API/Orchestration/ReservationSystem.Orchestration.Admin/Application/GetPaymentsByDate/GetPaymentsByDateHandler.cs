using ReservationSystem.Orchestration.Admin.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Admin.Models.Responses;

namespace ReservationSystem.Orchestration.Admin.Application.GetPaymentsByDate;

public sealed class GetPaymentsByDateHandler
{
    private readonly PaymentServiceClient _paymentServiceClient;

    public GetPaymentsByDateHandler(PaymentServiceClient paymentServiceClient)
    {
        _paymentServiceClient = paymentServiceClient;
    }

    public async Task<IReadOnlyList<AdminPaymentListItemResponse>> HandleAsync(GetPaymentsByDateQuery query, CancellationToken cancellationToken)
    {
        var payments = await _paymentServiceClient.GetPaymentsByDateAsync(query.Date, cancellationToken);

        return payments.Select(p => new AdminPaymentListItemResponse
        {
            PaymentId        = p.PaymentId,
            BookingReference = p.BookingReference,
            Method           = p.Method,
            CardType         = p.CardType,
            CardLast4        = p.CardLast4,
            CurrencyCode     = p.CurrencyCode,
            Amount           = p.Amount,
            AuthorisedAmount = p.AuthorisedAmount,
            SettledAmount    = p.SettledAmount,
            Status           = p.Status,
            AuthorisedAt     = p.AuthorisedAt,
            SettledAt        = p.SettledAt,
            Description      = p.Description,
            CreatedAt        = p.CreatedAt,
            UpdatedAt        = p.UpdatedAt,
            EventCount       = p.EventCount
        }).ToList();
    }
}

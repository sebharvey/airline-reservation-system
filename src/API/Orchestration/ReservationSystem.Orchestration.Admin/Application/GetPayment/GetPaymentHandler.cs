using ReservationSystem.Orchestration.Admin.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Admin.Models.Responses;

namespace ReservationSystem.Orchestration.Admin.Application.GetPayment;

public sealed class GetPaymentHandler
{
    private readonly PaymentServiceClient _paymentServiceClient;

    public GetPaymentHandler(PaymentServiceClient paymentServiceClient)
    {
        _paymentServiceClient = paymentServiceClient;
    }

    public async Task<AdminPaymentResponse?> HandleAsync(GetPaymentQuery query, CancellationToken cancellationToken)
    {
        var payment = await _paymentServiceClient.GetPaymentAsync(query.PaymentId, cancellationToken);

        if (payment is null)
            return null;

        return new AdminPaymentResponse
        {
            PaymentId        = payment.PaymentId,
            BookingReference = payment.BookingReference,
            Method           = payment.Method,
            CardType         = payment.CardType,
            CardLast4        = payment.CardLast4,
            CurrencyCode     = payment.CurrencyCode,
            Amount           = payment.Amount,
            AuthorisedAmount = payment.AuthorisedAmount,
            SettledAmount    = payment.SettledAmount,
            Status           = payment.Status,
            AuthorisedAt     = payment.AuthorisedAt,
            SettledAt        = payment.SettledAt,
            Description      = payment.Description,
            CreatedAt        = payment.CreatedAt,
            UpdatedAt        = payment.UpdatedAt
        };
    }
}

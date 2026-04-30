using ReservationSystem.Orchestration.Admin.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Admin.Models.Responses;

namespace ReservationSystem.Orchestration.Admin.Application.GetPaymentEvents;

public sealed class GetPaymentEventsHandler
{
    private readonly PaymentServiceClient _paymentServiceClient;

    public GetPaymentEventsHandler(PaymentServiceClient paymentServiceClient)
    {
        _paymentServiceClient = paymentServiceClient;
    }

    public async Task<IReadOnlyList<AdminPaymentEventResponse>?> HandleAsync(GetPaymentEventsQuery query, CancellationToken cancellationToken)
    {
        var payment = await _paymentServiceClient.GetPaymentAsync(query.PaymentId, cancellationToken);

        if (payment is null)
            return null;

        var events = await _paymentServiceClient.GetPaymentEventsAsync(query.PaymentId, cancellationToken);

        return events.Select(e => new AdminPaymentEventResponse
        {
            PaymentEventId = e.PaymentEventId,
            PaymentId      = e.PaymentId,
            EventType      = e.EventType,
            ProductType    = e.ProductType,
            Amount         = e.Amount,
            CurrencyCode   = e.CurrencyCode,
            Notes          = e.Notes,
            CreatedAt      = e.CreatedAt
        }).ToList();
    }
}

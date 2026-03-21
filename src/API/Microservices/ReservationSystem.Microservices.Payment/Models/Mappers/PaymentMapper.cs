using ReservationSystem.Microservices.Payment.Domain.Entities;
using ReservationSystem.Microservices.Payment.Models.Responses;
using Payment = ReservationSystem.Microservices.Payment.Domain.Entities.Payment;

namespace ReservationSystem.Microservices.Payment.Models.Mappers;

/// <summary>
/// Static mapping methods between all model representations of a Payment.
///
/// Mapping directions:
///
///   Domain entity → HTTP response
///
/// Static methods are used deliberately — no state, no DI overhead, trivially testable.
/// </summary>
public static class PaymentMapper
{
    // -------------------------------------------------------------------------
    // Domain entity → HTTP response
    // -------------------------------------------------------------------------

    public static AuthorisePaymentResponse ToAuthoriseResponse(Payment payment) =>
        new()
        {
            PaymentReference = payment.PaymentReference,
            AuthorisedAmount = payment.AuthorisedAmount,
            Status = payment.Status
        };

    public static SettlePaymentResponse ToSettleResponse(Payment payment) =>
        new()
        {
            PaymentReference = payment.PaymentReference,
            SettledAmount = payment.SettledAmount ?? 0m,
            SettledAt = payment.SettledAt
        };

    public static RefundPaymentResponse ToRefundResponse(Payment payment, decimal refundedAmount) =>
        new()
        {
            PaymentReference = payment.PaymentReference,
            RefundedAmount = refundedAmount,
            Status = payment.Status
        };
}

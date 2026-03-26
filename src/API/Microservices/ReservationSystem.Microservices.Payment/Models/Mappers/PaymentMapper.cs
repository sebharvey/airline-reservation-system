using ReservationSystem.Microservices.Payment.Models.Responses;

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

    public static InitialisePaymentResponse ToInitialiseResponse(Domain.Entities.Payment payment) =>
        new()
        {
            PaymentId = payment.PaymentId,
            Amount = payment.Amount,
            Status = payment.Status
        };

    public static AuthorisePaymentResponse ToAuthoriseResponse(Domain.Entities.Payment payment) =>
        new()
        {
            PaymentId = payment.PaymentId,
            AuthorisedAmount = payment.AuthorisedAmount ?? 0m,
            Status = payment.Status
        };

    public static SettlePaymentResponse ToSettleResponse(Domain.Entities.Payment payment) =>
        new()
        {
            PaymentId = payment.PaymentId,
            SettledAmount = payment.SettledAmount ?? 0m,
            SettledAt = payment.SettledAt
        };

    public static RefundPaymentResponse ToRefundResponse(Domain.Entities.Payment payment, decimal refundedAmount) =>
        new()
        {
            PaymentId = payment.PaymentId,
            RefundedAmount = refundedAmount,
            Status = payment.Status
        };

    public static VoidPaymentResponse ToVoidResponse(Domain.Entities.Payment payment) =>
        new()
        {
            PaymentId = payment.PaymentId,
            Status = payment.Status
        };
}

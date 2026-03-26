using ReservationSystem.Microservices.Payment.Domain.Entities;
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

    public static PaymentResponse ToPaymentResponse(Domain.Entities.Payment payment) =>
        new()
        {
            PaymentId = payment.PaymentId,
            BookingReference = payment.BookingReference,
            PaymentType = payment.PaymentType,
            Method = payment.Method,
            CardType = payment.CardType,
            CardLast4 = payment.CardLast4,
            CurrencyCode = payment.CurrencyCode,
            Amount = payment.Amount,
            AuthorisedAmount = payment.AuthorisedAmount,
            SettledAmount = payment.SettledAmount,
            Status = payment.Status,
            AuthorisedAt = payment.AuthorisedAt,
            SettledAt = payment.SettledAt,
            Description = payment.Description,
            CreatedAt = payment.CreatedAt,
            UpdatedAt = payment.UpdatedAt
        };

    public static PaymentEventResponse ToPaymentEventResponse(PaymentEvent paymentEvent) =>
        new()
        {
            PaymentEventId = paymentEvent.PaymentEventId,
            PaymentId = paymentEvent.PaymentId,
            EventType = paymentEvent.EventType,
            Amount = paymentEvent.Amount,
            CurrencyCode = paymentEvent.CurrencyCode,
            Notes = paymentEvent.Notes,
            CreatedAt = paymentEvent.CreatedAt
        };
}

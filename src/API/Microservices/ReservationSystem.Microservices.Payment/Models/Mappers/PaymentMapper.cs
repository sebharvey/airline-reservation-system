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

    public static AuthorisePaymentResponse ToAuthoriseResponse(Payment payment) =>
        new()
        {
            PaymentId = payment.PaymentId,
            PaymentReference = payment.PaymentReference,
            BookingReference = payment.BookingReference,
            PaymentType = payment.PaymentType,
            Method = payment.Method,
            CardType = payment.CardType,
            CardLast4 = payment.CardLast4,
            CurrencyCode = payment.CurrencyCode,
            AuthorisedAmount = payment.AuthorisedAmount,
            Status = payment.Status,
            AuthorisedAt = payment.AuthorisedAt,
            Description = payment.Description,
            CreatedAt = payment.CreatedAt,
            UpdatedAt = payment.UpdatedAt
        };

    public static SettlePaymentResponse ToSettleResponse(Payment payment) =>
        new()
        {
            PaymentId = payment.PaymentId,
            PaymentReference = payment.PaymentReference,
            CurrencyCode = payment.CurrencyCode,
            AuthorisedAmount = payment.AuthorisedAmount,
            SettledAmount = payment.SettledAmount,
            Status = payment.Status,
            AuthorisedAt = payment.AuthorisedAt,
            SettledAt = payment.SettledAt,
            UpdatedAt = payment.UpdatedAt
        };

    public static RefundPaymentResponse ToRefundResponse(Payment payment) =>
        new()
        {
            PaymentId = payment.PaymentId,
            PaymentReference = payment.PaymentReference,
            CurrencyCode = payment.CurrencyCode,
            AuthorisedAmount = payment.AuthorisedAmount,
            Status = payment.Status,
            UpdatedAt = payment.UpdatedAt
        };
}

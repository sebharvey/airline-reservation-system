namespace ReservationSystem.Microservices.Payment.Application.UpdateBookingReference;

/// <summary>
/// Command to link a confirmed booking reference to a payment record.
/// Called after order confirmation when the booking reference is first assigned.
/// </summary>
public sealed record UpdateBookingReferenceCommand(
    Guid PaymentId,
    string BookingReference);

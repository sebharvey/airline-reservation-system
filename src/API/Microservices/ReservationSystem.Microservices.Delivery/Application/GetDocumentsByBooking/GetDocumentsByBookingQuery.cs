namespace ReservationSystem.Microservices.Delivery.Application.GetDocumentsByBooking;

/// <summary>
/// Query to retrieve all documents associated with a booking reference.
/// </summary>
public sealed record GetDocumentsByBookingQuery(string BookingReference);

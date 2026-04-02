namespace ReservationSystem.Microservices.Customer.Application.AddCustomerOrder;

public sealed record AddCustomerOrderCommand(
    string LoyaltyNumber,
    Guid OrderId,
    string BookingReference);

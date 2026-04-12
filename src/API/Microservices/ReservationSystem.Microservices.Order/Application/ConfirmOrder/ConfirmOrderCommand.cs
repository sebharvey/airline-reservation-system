namespace ReservationSystem.Microservices.Order.Application.ConfirmOrder;

public sealed record ConfirmOrderCommand(
    Guid OrderId,
    Guid BasketId,
    string PaymentReferencesJson,
    string? EnrichedOffersJson = null);

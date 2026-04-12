namespace ReservationSystem.Microservices.Ancillary.Application.Product.UpdateProductPrice;

public sealed record UpdateProductPriceCommand(
    Guid PriceId,
    decimal Price,
    decimal Tax,
    bool IsActive);

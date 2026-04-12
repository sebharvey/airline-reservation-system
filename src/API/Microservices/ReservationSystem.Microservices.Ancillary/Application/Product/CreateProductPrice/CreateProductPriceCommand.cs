namespace ReservationSystem.Microservices.Ancillary.Application.Product.CreateProductPrice;

public sealed record CreateProductPriceCommand(
    Guid ProductId,
    string CurrencyCode,
    decimal Price,
    decimal Tax);

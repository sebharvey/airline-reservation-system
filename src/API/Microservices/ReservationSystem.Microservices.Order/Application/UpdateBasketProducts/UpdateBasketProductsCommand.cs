namespace ReservationSystem.Microservices.Order.Application.UpdateBasketProducts;

/// <summary>
/// Command to add or replace product ancillary selections in a basket.
/// </summary>
public sealed record UpdateBasketProductsCommand(Guid BasketId, string ProductsData);

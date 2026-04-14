using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Order.Domain.Entities;
using ReservationSystem.Microservices.Order.Domain.Repositories;

namespace ReservationSystem.Microservices.Order.Application.UpdateBasketProducts;

/// <summary>
/// Handles the <see cref="UpdateBasketProductsCommand"/>.
/// Adds or replaces product ancillary selections within an existing basket.
/// </summary>
public sealed class UpdateBasketProductsHandler
{
    private readonly IBasketRepository _repository;
    private readonly ILogger<UpdateBasketProductsHandler> _logger;

    public UpdateBasketProductsHandler(
        IBasketRepository repository,
        ILogger<UpdateBasketProductsHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Basket?> HandleAsync(
        UpdateBasketProductsCommand command,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating products for basket {BasketId}", command.BasketId);

        var basket = await _repository.GetByIdAsync(command.BasketId, cancellationToken);
        if (basket is null)
        {
            _logger.LogWarning("Basket {BasketId} not found", command.BasketId);
            return null;
        }

        if (basket.BasketStatus != BasketStatusValues.Active)
        {
            _logger.LogWarning("Basket {BasketId} is not open (status: {Status})", command.BasketId, basket.BasketStatus);
            throw new InvalidOperationException($"Basket is not open. Current status: {basket.BasketStatus}");
        }

        var basketJson = JsonNode.Parse(basket.BasketData)?.AsObject() ?? new JsonObject();
        var productsNode = JsonNode.Parse(command.ProductsData);
        basketJson["products"] = productsNode;

        // Calculate total product amount from product selections
        decimal totalProductAmount = 0m;
        if (productsNode is JsonArray productsArray)
        {
            foreach (var product in productsArray)
                totalProductAmount += product?["price"]?.GetValue<decimal>() ?? 0m;
        }

        var totalAmount = (basket.TotalFareAmount ?? 0m) + basket.TotalSeatAmount + basket.TotalBagAmount + totalProductAmount;

        var updated = Basket.Reconstitute(
            basket.BasketId,
            basket.CurrencyCode,
            basket.BasketStatus,
            basket.TotalFareAmount,
            basket.TotalSeatAmount,
            basket.TotalBagAmount,
            totalAmount,
            basket.ExpiresAt,
            basket.ConfirmedOrderId,
            basket.Version + 1,
            basketJson.ToJsonString(),
            basket.CreatedAt,
            DateTime.UtcNow);

        await _repository.UpdateAsync(updated, cancellationToken);

        _logger.LogInformation("Basket {BasketId} products updated, totalProductAmount={TotalProductAmount}",
            command.BasketId, totalProductAmount);

        return updated;
    }
}

using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Order.Domain.Entities;
using ReservationSystem.Microservices.Order.Domain.Repositories;

namespace ReservationSystem.Microservices.Order.Application.UpdateBasketBags;

/// <summary>
/// Handles the <see cref="UpdateBasketBagsCommand"/>.
/// Adds or replaces bag ancillary selections within an existing basket.
/// </summary>
public sealed class UpdateBasketBagsHandler
{
    private readonly IBasketRepository _repository;
    private readonly ILogger<UpdateBasketBagsHandler> _logger;

    public UpdateBasketBagsHandler(
        IBasketRepository repository,
        ILogger<UpdateBasketBagsHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Basket?> HandleAsync(
        UpdateBasketBagsCommand command,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating bags for basket {BasketId}", command.BasketId);

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
        var bagsNode = JsonNode.Parse(command.BagsData);
        basketJson["bags"] = bagsNode;

        // Calculate total bag amount from bag selections
        decimal totalBagAmount = 0m;
        if (bagsNode is JsonArray bagsArray)
        {
            foreach (var bag in bagsArray)
            {
                var price = bag?["price"]?.GetValue<decimal>() ?? 0m;
                totalBagAmount += price;
            }
        }

        var totalAmount = (basket.TotalFareAmount ?? 0m) + basket.TotalSeatAmount + totalBagAmount;

        var updated = Basket.Reconstitute(
            basket.BasketId,
            basket.ChannelCode,
            basket.CurrencyCode,
            basket.BasketStatus,
            basket.TotalFareAmount,
            basket.TotalSeatAmount,
            totalBagAmount,
            totalAmount,
            basket.ExpiresAt,
            basket.ConfirmedOrderId,
            basket.Version + 1,
            basketJson.ToJsonString(),
            basket.CreatedAt,
            DateTimeOffset.UtcNow);

        await _repository.UpdateAsync(updated, cancellationToken);

        _logger.LogInformation("Basket {BasketId} bags updated, totalBagAmount={TotalBagAmount}",
            command.BasketId, totalBagAmount);

        return updated;
    }
}

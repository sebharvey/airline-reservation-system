using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Order.Domain.Entities;
using ReservationSystem.Microservices.Order.Domain.Repositories;

namespace ReservationSystem.Microservices.Order.Application.AddBasketOffer;

/// <summary>
/// Handles the <see cref="AddBasketOfferCommand"/>.
/// Validates offer expiry, assigns a basket item ID, appends the offer to the basket's
/// flight offers collection, and persists the updated basket.
/// </summary>
public sealed class AddBasketOfferHandler
{
    private readonly IBasketRepository _repository;
    private readonly ILogger<AddBasketOfferHandler> _logger;

    public AddBasketOfferHandler(
        IBasketRepository repository,
        ILogger<AddBasketOfferHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<AddBasketOfferResult?> HandleAsync(
        AddBasketOfferCommand command,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Adding offer to basket {BasketId}", command.BasketId);

        var basket = await _repository.GetByIdAsync(command.BasketId, cancellationToken);
        if (basket is null)
        {
            _logger.LogWarning("Basket {BasketId} not found", command.BasketId);
            return null;
        }

        // Validate offer expiry
        var offerNode = JsonNode.Parse(command.OfferJson);
        if (offerNode is JsonObject offerObj &&
            offerObj["offerExpiresAt"] is JsonNode expiresAtNode)
        {
            var offerExpiresAt = expiresAtNode.GetValue<DateTime>();
            if (offerExpiresAt <= DateTime.UtcNow)
                throw new InvalidOperationException("Offer has expired and is no longer available.");
        }

        if (basket.BasketStatus != BasketStatusValues.Active)
        {
            _logger.LogWarning("Basket {BasketId} is not open (status: {Status})", command.BasketId, basket.BasketStatus);
            throw new InvalidOperationException($"Basket is not open. Current status: {basket.BasketStatus}");
        }

        // Build the updated offers array: existing offers + new offer appended
        var nextItemNumber = 1;
        var offersArray = new JsonArray();

        if (!string.IsNullOrEmpty(basket.BasketData))
        {
            var basketNode = JsonNode.Parse(basket.BasketData)?.AsObject();
            if (basketNode?["flightOffers"] is JsonArray existing)
            {
                nextItemNumber = existing.Count + 1;
                foreach (var item in existing)
                    offersArray.Add(item?.DeepClone());
            }
        }

        // Assign basketItemId and append the new offer
        var basketItemId = $"BI-{nextItemNumber}";
        var newOffer = (offerNode?.DeepClone() ?? JsonNode.Parse(command.OfferJson)!).AsObject();
        newOffer["basketItemId"] = basketItemId;
        offersArray.Add(newOffer);

        // Update basket JSON and recalculate totals
        var basketJson = JsonNode.Parse(basket.BasketData)?.AsObject() ?? new JsonObject();
        basketJson["flightOffers"] = offersArray.DeepClone();

        decimal totalFareAmount = 0m;
        foreach (var offer in offersArray)
        {
            var price = offer?["totalAmount"]?.GetValue<decimal>() ?? 0m;
            totalFareAmount += price;
        }

        var totalAmount = totalFareAmount + basket.TotalSeatAmount + basket.TotalBagAmount;

        var updated = Basket.Reconstitute(
            basket.BasketId,
            basket.CurrencyCode,
            basket.BasketStatus,
            totalFareAmount,
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

        _logger.LogInformation(
            "Offer added to basket {BasketId} as {BasketItemId}, totalFareAmount={TotalFareAmount}",
            command.BasketId, basketItemId, totalFareAmount);

        return new AddBasketOfferResult(basket.BasketId, basketItemId, totalFareAmount, totalAmount);
    }
}

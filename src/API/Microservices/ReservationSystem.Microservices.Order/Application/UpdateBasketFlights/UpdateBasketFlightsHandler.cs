using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Order.Domain.Entities;
using ReservationSystem.Microservices.Order.Domain.Repositories;

namespace ReservationSystem.Microservices.Order.Application.UpdateBasketFlights;

/// <summary>
/// Handles the <see cref="UpdateBasketFlightsCommand"/>.
/// Adds or replaces flight selections within an existing basket.
/// </summary>
public sealed class UpdateBasketFlightsHandler
{
    private readonly IBasketRepository _repository;
    private readonly ILogger<UpdateBasketFlightsHandler> _logger;

    public UpdateBasketFlightsHandler(
        IBasketRepository repository,
        ILogger<UpdateBasketFlightsHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Basket?> HandleAsync(
        UpdateBasketFlightsCommand command,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating flights for basket {BasketId}", command.BasketId);

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
        var flightsNode = JsonNode.Parse(command.FlightsData);
        basketJson["flightOffers"] = flightsNode;

        // Calculate total fare amount from flight offers (totalAmount is a top-level field on each offer)
        decimal totalFareAmount = 0m;
        if (flightsNode is JsonArray flightsArray)
        {
            foreach (var offer in flightsArray)
            {
                var price = offer?["totalAmount"]?.GetValue<decimal>() ?? 0m;
                totalFareAmount += price;
            }
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

        _logger.LogInformation("Basket {BasketId} flights updated, totalFareAmount={TotalFareAmount}",
            command.BasketId, totalFareAmount);

        return updated;
    }
}

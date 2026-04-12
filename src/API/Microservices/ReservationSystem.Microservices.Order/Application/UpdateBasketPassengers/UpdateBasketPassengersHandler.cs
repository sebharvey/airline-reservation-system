using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Order.Domain.Entities;
using ReservationSystem.Microservices.Order.Domain.Repositories;

namespace ReservationSystem.Microservices.Order.Application.UpdateBasketPassengers;

/// <summary>
/// Handles the <see cref="UpdateBasketPassengersCommand"/>.
/// Updates passenger details within an existing basket.
/// </summary>
public sealed class UpdateBasketPassengersHandler
{
    private readonly IBasketRepository _repository;
    private readonly ILogger<UpdateBasketPassengersHandler> _logger;

    public UpdateBasketPassengersHandler(
        IBasketRepository repository,
        ILogger<UpdateBasketPassengersHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Basket?> HandleAsync(
        UpdateBasketPassengersCommand command,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating passengers for basket {BasketId}", command.BasketId);

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
        var passengersNode = JsonNode.Parse(command.PassengersData);
        basketJson["passengers"] = passengersNode;

        // Recalculate fare totals from the stored per-person unit amounts multiplied by the
        // actual number of passengers being set. This ensures the charged amount is always
        // authoritative regardless of the passengerCount supplied at basket creation time.
        var passengerCount = passengersNode is JsonArray paxArray ? paxArray.Count : 0;
        decimal totalFareAmount = 0m;

        if (passengerCount > 0 && basketJson["flightOffers"] is JsonArray flightOffers)
        {
            foreach (var offerNode in flightOffers)
            {
                if (offerNode is not JsonObject offer) continue;

                var unitAmount    = offer["unitAmount"]?.GetValue<decimal>() ?? 0m;
                var unitBaseFare = offer["unitBaseFareAmount"]?.GetValue<decimal>() ?? 0m;
                var unitTax      = offer["unitTaxAmount"]?.GetValue<decimal>() ?? 0m;

                offer["totalAmount"]    = unitAmount * passengerCount;
                offer["baseFareAmount"] = unitBaseFare * passengerCount;
                offer["taxAmount"]      = unitTax * passengerCount;
                offer["passengerCount"] = passengerCount;

                if (offer["unitPointsPrice"] is JsonNode unitPointsNode)
                    offer["pointsPrice"] = unitPointsNode.GetValue<int>() * passengerCount;
                if (offer["unitPointsTaxes"] is JsonNode unitPointsTaxNode)
                    offer["pointsTaxes"] = unitPointsTaxNode.GetValue<decimal>() * passengerCount;

                totalFareAmount += unitAmount * passengerCount;
            }
        }
        else
        {
            totalFareAmount = basket.TotalFareAmount ?? 0m;
        }

        var totalAmount = totalFareAmount + basket.TotalSeatAmount + basket.TotalBagAmount;

        var updated = Basket.Reconstitute(
            basket.BasketId,
            basket.ChannelCode,
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

        _logger.LogInformation("Basket {BasketId} passengers updated, passengerCount={PassengerCount}, totalFareAmount={TotalFareAmount}",
            command.BasketId, passengerCount, totalFareAmount);

        return updated;
    }
}

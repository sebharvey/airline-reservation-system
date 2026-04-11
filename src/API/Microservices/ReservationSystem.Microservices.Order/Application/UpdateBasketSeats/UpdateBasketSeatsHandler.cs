using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Order.Domain.Entities;
using ReservationSystem.Microservices.Order.Domain.Repositories;

namespace ReservationSystem.Microservices.Order.Application.UpdateBasketSeats;

/// <summary>
/// Handles the <see cref="UpdateBasketSeatsCommand"/>.
/// Adds or replaces seat selections within an existing basket.
/// </summary>
public sealed class UpdateBasketSeatsHandler
{
    private readonly IBasketRepository _repository;
    private readonly ILogger<UpdateBasketSeatsHandler> _logger;

    public UpdateBasketSeatsHandler(
        IBasketRepository repository,
        ILogger<UpdateBasketSeatsHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Basket?> HandleAsync(
        UpdateBasketSeatsCommand command,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating seats for basket {BasketId}", command.BasketId);

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
        var seatsNode = JsonNode.Parse(command.SeatsData);

        // Validate that each seat's cabin matches the booked cabin for its basket item
        if (seatsNode is JsonArray seatsForValidation)
        {
            var offerCabinByItemId = (basketJson["flightOffers"]?.AsArray() ?? [])
                .OfType<JsonObject>()
                .Where(o => o["basketItemId"]?.GetValue<string>() is not null)
                .ToDictionary(
                    o => o["basketItemId"]!.GetValue<string>(),
                    o => o["cabinCode"]?.GetValue<string>() ?? string.Empty,
                    StringComparer.OrdinalIgnoreCase);

            foreach (var seat in seatsForValidation)
            {
                var seatCabin = seat?["cabinCode"]?.GetValue<string>();
                if (string.IsNullOrEmpty(seatCabin)) continue;

                var itemRef = seat?["basketItemRef"]?.GetValue<string>();
                if (itemRef is null) continue;

                if (offerCabinByItemId.TryGetValue(itemRef, out var bookedCabin) &&
                    !string.Equals(seatCabin, bookedCabin, StringComparison.OrdinalIgnoreCase))
                {
                    var seatNumber = seat?["seatNumber"]?.GetValue<string>() ?? "unknown";
                    throw new InvalidOperationException(
                        $"Seat {seatNumber} is in cabin '{seatCabin}' but the booked cabin for basket item '{itemRef}' is '{bookedCabin}'.");
                }
            }
        }

        basketJson["seats"] = seatsNode;

        // Calculate total seat amount from seat selections
        decimal totalSeatAmount = 0m;
        if (seatsNode is JsonArray seatsArray)
        {
            foreach (var seat in seatsArray)
            {
                var priceNode = seat?["price"];
                decimal price = 0m;
                if (priceNode is not null)
                {
                    if (priceNode.GetValueKind() == System.Text.Json.JsonValueKind.String)
                        decimal.TryParse(priceNode.GetValue<string>(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out price);
                    else
                        price = priceNode.GetValue<decimal>();
                }
                totalSeatAmount += price;
            }
        }

        var totalAmount = (basket.TotalFareAmount ?? 0m) + totalSeatAmount + basket.TotalBagAmount;

        var updated = Basket.Reconstitute(
            basket.BasketId,
            basket.ChannelCode,
            basket.CurrencyCode,
            basket.BasketStatus,
            basket.TotalFareAmount,
            totalSeatAmount,
            basket.TotalBagAmount,
            totalAmount,
            basket.ExpiresAt,
            basket.ConfirmedOrderId,
            basket.Version + 1,
            basketJson.ToJsonString(),
            basket.CreatedAt,
            DateTime.UtcNow);

        await _repository.UpdateAsync(updated, cancellationToken);

        _logger.LogInformation("Basket {BasketId} seats updated, totalSeatAmount={TotalSeatAmount}",
            command.BasketId, totalSeatAmount);

        return updated;
    }
}

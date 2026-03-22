using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Order.Domain.Entities;
using ReservationSystem.Microservices.Order.Domain.Repositories;

namespace ReservationSystem.Microservices.Order.Application.CreateBasket;

/// <summary>
/// Handles the <see cref="CreateBasketCommand"/>.
/// Creates and persists a new <see cref="Basket"/>.
/// </summary>
public sealed class CreateBasketHandler
{
    private readonly IBasketRepository _repository;
    private readonly ILogger<CreateBasketHandler> _logger;

    public CreateBasketHandler(
        IBasketRepository repository,
        ILogger<CreateBasketHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Basket> HandleAsync(
        CreateBasketCommand command,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating basket for channel {ChannelCode}, currency {CurrencyCode}",
            command.ChannelCode, command.CurrencyCode);

        var expiresAt = DateTime.UtcNow.AddMinutes(60);

        var basketData = new JsonObject
        {
            ["bookingType"] = "oneWay",
            ["channelCode"] = command.ChannelCode,
            ["currencyCode"] = command.CurrencyCode,
            ["flightOffers"] = new JsonArray(),
            ["passengers"] = new JsonArray(),
            ["seats"] = new JsonArray(),
            ["bags"] = new JsonArray()
        };

        var basket = Basket.Create(
            command.ChannelCode,
            command.CurrencyCode,
            expiresAt,
            basketData.ToJsonString());

        await _repository.CreateAsync(basket, cancellationToken);

        _logger.LogInformation("Basket {BasketId} created, expires at {ExpiresAt}",
            basket.BasketId, expiresAt);

        return basket;
    }
}

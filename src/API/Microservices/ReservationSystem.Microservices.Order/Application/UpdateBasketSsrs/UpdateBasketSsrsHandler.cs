using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Order.Domain.Entities;
using ReservationSystem.Microservices.Order.Domain.Repositories;

namespace ReservationSystem.Microservices.Order.Application.UpdateBasketSsrs;

/// <summary>
/// Handles the <see cref="UpdateBasketSsrsCommand"/>.
/// Adds or replaces SSR selections within an existing basket.
/// </summary>
public sealed class UpdateBasketSsrsHandler
{
    private readonly IBasketRepository _repository;
    private readonly ILogger<UpdateBasketSsrsHandler> _logger;

    public UpdateBasketSsrsHandler(
        IBasketRepository repository,
        ILogger<UpdateBasketSsrsHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Basket?> HandleAsync(
        UpdateBasketSsrsCommand command,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating SSRs for basket {BasketId}", command.BasketId);

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
        var ssrsNode = JsonNode.Parse(command.SsrsData);
        basketJson["ssrSelections"] = ssrsNode;

        var updated = Basket.Reconstitute(
            basket.BasketId,
            basket.CurrencyCode,
            basket.BasketStatus,
            basket.TotalFareAmount,
            basket.TotalSeatAmount,
            basket.TotalBagAmount,
            basket.TotalAmount,
            basket.ExpiresAt,
            basket.ConfirmedOrderId,
            basket.Version + 1,
            basketJson.ToJsonString(),
            basket.CreatedAt,
            DateTime.UtcNow);

        await _repository.UpdateAsync(updated, cancellationToken);

        _logger.LogInformation("Basket {BasketId} SSRs updated", command.BasketId);

        return updated;
    }
}

using System.Text.Json;
using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;

namespace ReservationSystem.Orchestration.Retail.Application.CreateBasket;

public sealed record CreateBasketResult(Guid BasketId);

public sealed class CreateBasketHandler
{
    private readonly OrderServiceClient _orderServiceClient;

    public CreateBasketHandler(OrderServiceClient orderServiceClient)
    {
        _orderServiceClient = orderServiceClient;
    }

    public async Task<CreateBasketResult> HandleAsync(CreateBasketCommand command, CancellationToken cancellationToken)
    {
        var bookingType = !string.IsNullOrWhiteSpace(command.BookingType)
            ? command.BookingType
            : command.LoyaltyNumber is not null ? "Reward" : "Revenue";

        var basket = await _orderServiceClient.CreateBasketAsync(
            channelCode: command.ChannelCode,
            currencyCode: command.CurrencyCode ?? "GBP",
            bookingType: bookingType,
            loyaltyNumber: command.LoyaltyNumber,
            totalPointsAmount: null,
            cancellationToken);

        foreach (var segment in command.Segments)
        {
            var offerJson = JsonSerializer.Serialize(new
            {
                offerId   = segment.OfferId,
                sessionId = segment.SessionId
            });
            await _orderServiceClient.AddOfferAsync(basket.BasketId, offerJson, cancellationToken);
        }

        return new CreateBasketResult(basket.BasketId);
    }
}

using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Retail.Models.Responses;

namespace ReservationSystem.Orchestration.Retail.Application.CreateBasket;

public sealed class CreateBasketHandler
{
    private readonly OrderServiceClient _orderServiceClient;

    public CreateBasketHandler(OrderServiceClient orderServiceClient)
    {
        _orderServiceClient = orderServiceClient;
    }

    public async Task<BasketResponse> HandleAsync(CreateBasketCommand command, CancellationToken cancellationToken)
    {
        var bookingType = command.LoyaltyNumber is not null ? "Reward" : "Revenue";

        var result = await _orderServiceClient.CreateBasketAsync(
            channelCode: "WEB",
            currencyCode: "GBP",
            bookingType: bookingType,
            loyaltyNumber: command.LoyaltyNumber,
            totalPointsAmount: null,
            cancellationToken);

        return new BasketResponse
        {
            BasketId = result.BasketId,
            Status = result.BasketStatus,
            CustomerId = command.CustomerId,
            TotalPrice = result.TotalAmount,
            Currency = result.CurrencyCode,
            ExpiresAt = result.ExpiresAt,
            CreatedAt = DateTime.UtcNow
        };
    }
}

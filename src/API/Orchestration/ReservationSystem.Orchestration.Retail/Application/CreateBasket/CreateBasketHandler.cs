using System.Text.Json;
using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;

namespace ReservationSystem.Orchestration.Retail.Application.CreateBasket;

public sealed record CreateBasketResult(Guid BasketId);

public sealed class CreateBasketHandler
{
    private readonly OrderServiceClient _orderServiceClient;
    private readonly OfferServiceClient _offerServiceClient;

    public CreateBasketHandler(OrderServiceClient orderServiceClient, OfferServiceClient offerServiceClient)
    {
        _orderServiceClient = orderServiceClient;
        _offerServiceClient = offerServiceClient;
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
            // Look up the stored offer to get pricing and flight details.
            // First try the session-scoped lookup (faster, indexed). If the session ID does not
            // match the stored offer (e.g. search returned empty and a random session was issued),
            // fall back to a session-unscoped lookup so flight details are always resolved when
            // the offer exists.
            var offerDetail = await _offerServiceClient.GetOfferAsync(segment.OfferId, segment.SessionId, cancellationToken);
            if (offerDetail is null)
                offerDetail = await _offerServiceClient.GetOfferAsync(segment.OfferId, null, cancellationToken);

            string offerJson;
            if (offerDetail is not null)
            {
                var offerItem = offerDetail.Offers.FirstOrDefault(o => o.OfferId == segment.OfferId)
                    ?? offerDetail.Offers.FirstOrDefault();

                offerJson = JsonSerializer.Serialize(new
                {
                    offerId        = segment.OfferId,
                    sessionId      = segment.SessionId,
                    inventoryId    = offerDetail.InventoryId,
                    flightNumber   = offerDetail.FlightNumber,
                    departureDate  = offerDetail.DepartureDate,
                    departureTime  = offerDetail.DepartureTime,
                    arrivalTime    = offerDetail.ArrivalTime,
                    origin         = offerDetail.Origin,
                    destination    = offerDetail.Destination,
                    aircraftType   = offerDetail.AircraftType,
                    offerExpiresAt = offerDetail.ExpiresAt,
                    cabinCode      = offerItem?.CabinCode,
                    fareBasisCode  = offerItem?.FareBasisCode,
                    fareFamily     = offerItem?.FareFamily,
                    totalAmount    = offerItem?.TotalAmount ?? 0m,
                    baseFareAmount = offerItem?.BaseFareAmount ?? 0m,
                    taxAmount      = offerItem?.TaxAmount ?? 0m,
                    isRefundable   = offerItem?.IsRefundable ?? false,
                    isChangeable   = offerItem?.IsChangeable ?? false,
                    pointsPrice    = offerItem?.PointsPrice,
                    pointsTaxes    = offerItem?.PointsTaxes
                });
            }
            else
            {
                // Offer not found — fall back to reference-only (totals will be 0)
                offerJson = JsonSerializer.Serialize(new
                {
                    offerId   = segment.OfferId,
                    sessionId = segment.SessionId
                });
            }

            await _orderServiceClient.AddOfferAsync(basket.BasketId, offerJson, cancellationToken);
        }

        return new CreateBasketResult(basket.BasketId);
    }
}

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

                var pax = command.PassengerCount;
                offerJson = JsonSerializer.Serialize(new
                {
                    offerId         = segment.OfferId,
                    sessionId       = segment.SessionId,
                    invId           = offerDetail.InventoryId,
                    flight          = offerDetail.FlightNumber,
                    depDate         = offerDetail.DepartureDate,
                    depTime         = offerDetail.DepartureTime,
                    arrTime         = offerDetail.ArrivalTime,
                    origin          = offerDetail.Origin,
                    dest            = offerDetail.Destination,
                    acType          = offerDetail.AircraftType,
                    offerExpiresAt  = offerDetail.ExpiresAt,
                    cabin           = offerItem?.CabinCode,
                    fareBasis       = offerItem?.FareBasisCode,
                    fareFamily      = offerItem?.FareFamily,
                    unitAmount      = offerItem?.TotalAmount ?? 0m,
                    unitBaseFare    = offerItem?.BaseFareAmount ?? 0m,
                    unitTax         = offerItem?.TaxAmount ?? 0m,
                    unitPointsPrice = offerItem?.PointsPrice,
                    unitPointsTaxes = offerItem?.PointsTaxes,
                    total           = (offerItem?.TotalAmount ?? 0m) * pax,
                    baseFare        = (offerItem?.BaseFareAmount ?? 0m) * pax,
                    tax             = (offerItem?.TaxAmount ?? 0m) * pax,
                    refundable      = offerItem?.IsRefundable ?? false,
                    changeable      = offerItem?.IsChangeable ?? false,
                    passengerCount  = pax,
                    pointsPrice     = offerItem?.PointsPrice != null ? offerItem.PointsPrice * pax : (int?)null,
                    pointsTaxes     = offerItem?.PointsTaxes != null ? offerItem.PointsTaxes * pax : (decimal?)null
                });
            }
            else
            {
                throw new InvalidOperationException(
                    $"Offer '{segment.OfferId}' could not be found or has expired. Search for flights and select a current offer before creating a basket.");
            }

            await _orderServiceClient.AddOfferAsync(basket.BasketId, offerJson, cancellationToken);
        }

        return new CreateBasketResult(basket.BasketId);
    }
}

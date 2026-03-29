using System.Text.Json;
using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices.Dto;
using ReservationSystem.Orchestration.Retail.Models.Responses;

namespace ReservationSystem.Orchestration.Retail.Application.CreateBasket;

public sealed class CreateBasketHandler
{
    private readonly OfferServiceClient _offerServiceClient;
    private readonly OrderServiceClient _orderServiceClient;

    public CreateBasketHandler(
        OfferServiceClient offerServiceClient,
        OrderServiceClient orderServiceClient)
    {
        _offerServiceClient = offerServiceClient;
        _orderServiceClient = orderServiceClient;
    }

    public async Task<BasketResponse> HandleAsync(CreateBasketCommand command, CancellationToken cancellationToken)
    {
        // 1. Validate all offers: must exist, be unconsumed, and not expired
        var offerDetails = new List<OfferDetailDto>();
        foreach (var offerId in command.OfferIds)
        {
            var offer = await _offerServiceClient.GetOfferAsync(offerId, cancellationToken);
            if (offer is null)
                throw new KeyNotFoundException($"Offer {offerId} not found.");
            if (offer.IsConsumed)
                throw new InvalidOperationException($"Offer {offerId} has already been consumed.");
            if (DateTime.TryParse(offer.ExpiresAt, out var offerExpiry) && offerExpiry <= DateTime.UtcNow)
                throw new InvalidOperationException($"Offer {offerId} has expired.");
            offerDetails.Add(offer);
        }

        // 2. Hold inventory for each validated offer; release all on partial failure
        var heldOfferIds = new List<Guid>();
        try
        {
            foreach (var offer in offerDetails)
            {
                await _offerServiceClient.HoldInventoryAsync(offer.OfferId, cancellationToken);
                heldOfferIds.Add(offer.OfferId);
            }
        }
        catch
        {
            foreach (var heldId in heldOfferIds)
                await _offerServiceClient.ReleaseInventoryAsync(heldId, CancellationToken.None);
            throw;
        }

        // 3. Create basket record in Order MS
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

        // 4. Add each validated offer snapshot to the basket
        var flights = new List<BasketFlight>();
        foreach (var offer in offerDetails)
        {
            var offerJson = JsonSerializer.Serialize(new
            {
                offerId = offer.OfferId,
                inventoryId = offer.InventoryId,
                flightNumber = offer.FlightNumber,
                departureDate = offer.DepartureDate,
                departureTime = offer.DepartureTime,
                arrivalTime = offer.ArrivalTime,
                arrivalDayOffset = offer.ArrivalDayOffset,
                origin = offer.Origin,
                destination = offer.Destination,
                aircraftType = offer.AircraftType,
                cabinCode = offer.CabinCode,
                fareBasisCode = offer.FareBasisCode,
                fareFamily = offer.FareFamily,
                currencyCode = offer.CurrencyCode,
                baseFareAmount = offer.BaseFareAmount,
                taxAmount = offer.TaxAmount,
                totalAmount = offer.TotalAmount,
                isRefundable = offer.IsRefundable,
                isChangeable = offer.IsChangeable,
                offerExpiresAt = offer.ExpiresAt
            });

            await _orderServiceClient.AddOfferAsync(basket.BasketId, offerJson, cancellationToken);

            flights.Add(new BasketFlight
            {
                OfferId = offer.OfferId,
                FlightNumber = offer.FlightNumber,
                Origin = offer.Origin,
                Destination = offer.Destination,
                DepartureDateTime = ParseOfferDateTime(offer.DepartureDate, offer.DepartureTime),
                ArrivalDateTime = ParseArrivalDateTime(offer.DepartureDate, offer.ArrivalTime, offer.ArrivalDayOffset),
                CabinCode = offer.CabinCode,
                FareFamily = offer.FareFamily,
                TotalAmount = offer.TotalAmount
            });
        }

        var totalFareAmount = offerDetails.Sum(o => o.TotalAmount);

        return new BasketResponse
        {
            BasketId = basket.BasketId,
            Status = basket.BasketStatus,
            BookingType = bookingType,
            CustomerId = command.CustomerId,
            Flights = flights,
            TotalFareAmount = totalFareAmount,
            TotalSeatAmount = 0m,
            TotalBagAmount = 0m,
            TotalPrice = totalFareAmount,
            Currency = basket.CurrencyCode,
            ExpiresAt = basket.ExpiresAt,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static DateTime ParseOfferDateTime(string date, string time) =>
        DateTime.TryParse($"{date}T{time}", out var dt) ? dt : default;

    private static DateTime ParseArrivalDateTime(string departureDate, string arrivalTime, int dayOffset)
    {
        if (!DateTime.TryParse($"{departureDate}T{arrivalTime}", out var dt)) return default;
        return dt.AddDays(dayOffset);
    }
}

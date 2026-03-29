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
        // 1. Validate all offers: must exist and not expired
        var offerDetails = new List<OfferDetailDto>();
        foreach (var offerId in command.OfferIds)
        {
            var offer = await _offerServiceClient.GetOfferAsync(offerId, command.SessionId, cancellationToken);
            if (offer is null)
                throw new KeyNotFoundException($"Offer {offerId} not found.");
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
                inventoryId      = offer.InventoryId,
                flightNumber     = offer.FlightNumber,
                departureDate    = offer.DepartureDate,
                departureTime    = offer.DepartureTime,
                arrivalTime      = offer.ArrivalTime,
                arrivalDayOffset = offer.ArrivalDayOffset,
                origin           = offer.Origin,
                destination      = offer.Destination,
                aircraftType     = offer.AircraftType,
                offerExpiresAt   = offer.ExpiresAt,
                offers           = offer.Offers
            });

            await _orderServiceClient.AddOfferAsync(basket.BasketId, offerJson, cancellationToken);

            // Use the lowest-priced offer item for basket summary display.
            var cheapest = offer.Offers.OrderBy(o => o.TotalAmount).FirstOrDefault();
            flights.Add(new BasketFlight
            {
                OfferId           = cheapest?.OfferId ?? Guid.Empty,
                FlightNumber      = offer.FlightNumber,
                Origin            = offer.Origin,
                Destination       = offer.Destination,
                DepartureDateTime = ParseOfferDateTime(offer.DepartureDate, offer.DepartureTime),
                ArrivalDateTime   = ParseArrivalDateTime(offer.DepartureDate, offer.ArrivalTime, offer.ArrivalDayOffset),
                CabinCode         = cheapest?.CabinCode ?? string.Empty,
                FareFamily        = cheapest?.FareFamily,
                TotalAmount       = cheapest?.TotalAmount ?? 0m
            });
        }

        var totalFareAmount = offerDetails
            .Select(o => o.Offers.OrderBy(x => x.TotalAmount).FirstOrDefault()?.TotalAmount ?? 0m)
            .Sum();

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

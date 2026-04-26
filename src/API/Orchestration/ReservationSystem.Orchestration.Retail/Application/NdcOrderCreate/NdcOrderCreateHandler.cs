using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using ReservationSystem.Orchestration.Retail.Application.ConfirmBasket;
using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices.Dto;
using ReservationSystem.Orchestration.Retail.Models.Responses;

namespace ReservationSystem.Orchestration.Retail.Application.NdcOrderCreate;

public enum NdcOrderCreateOutcome { Success, OfferNotFound, OfferExpired }

public sealed record NdcOrderCreateResult(
    NdcOrderCreateOutcome Outcome,
    OrderResponse? Order = null,
    OfferDetailDto? OfferDetail = null);

/// <summary>
/// Handles POST /v1/ndc/OrderCreate.
///
/// Orchestrates the full booking flow for an NDC client in a single request:
///   1. Validates and re-prices the stored offer via the Offer microservice.
///   2. Creates a transient basket in the Order microservice.
///   3. Adds the offer to the basket using the same JSON shape as the web basket flow.
///   4. Updates named passenger data on the basket.
///   5. Delegates to ConfirmBasketHandler to process payment, issue e-tickets,
///      write the manifest, and return a fully confirmed OrderResponse.
/// </summary>
public sealed class NdcOrderCreateHandler
{
    private readonly OrderServiceClient _orderServiceClient;
    private readonly OfferServiceClient _offerServiceClient;
    private readonly ConfirmBasketHandler _confirmBasketHandler;
    private readonly ILogger<NdcOrderCreateHandler> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public NdcOrderCreateHandler(
        OrderServiceClient orderServiceClient,
        OfferServiceClient offerServiceClient,
        ConfirmBasketHandler confirmBasketHandler,
        ILogger<NdcOrderCreateHandler> logger)
    {
        _orderServiceClient = orderServiceClient;
        _offerServiceClient = offerServiceClient;
        _confirmBasketHandler = confirmBasketHandler;
        _logger = logger;
    }

    public async Task<NdcOrderCreateResult> HandleAsync(
        NdcOrderCreateCommand command,
        CancellationToken cancellationToken)
    {
        // 1. Reprice the stored offer to confirm it is still valid and get current fares.
        var reprice = await _offerServiceClient.RepriceOfferAsync(
            command.OfferRefId, cancellationToken: cancellationToken);

        if (reprice is null)
            return new NdcOrderCreateResult(NdcOrderCreateOutcome.OfferNotFound);

        if (!reprice.Validated)
            return new NdcOrderCreateResult(NdcOrderCreateOutcome.OfferExpired);

        // 2. Fetch full offer detail for flight data (origin, destination, times, aircraft).
        var offerDetail = await _offerServiceClient.GetOfferAsync(
            command.OfferRefId, cancellationToken: cancellationToken);

        if (offerDetail is null)
            return new NdcOrderCreateResult(NdcOrderCreateOutcome.OfferNotFound);

        var paxCount = command.Passengers.Count < 1 ? 1 : command.Passengers.Count;

        // 3. Create a transient basket in the Order MS.
        var basket = await _orderServiceClient.CreateBasketAsync(
            currency: "GBP",
            bookingType: "Revenue",
            loyaltyNumber: null,
            totalPointsAmount: null,
            cancellationToken);

        _logger.LogInformation(
            "[NDC] OrderCreate basket created: {BasketId} for offer {OfferId}",
            basket.BasketId, command.OfferRefId);

        // 4. Add the stored offer to the basket.
        //    The JSON shape must match CreateBasketHandler so that ConfirmBasketHandler
        //    can parse flightOffers from BasketData correctly.
        var offerItem = offerDetail.Offers.FirstOrDefault(o => o.OfferId == command.OfferRefId)
            ?? offerDetail.Offers.FirstOrDefault();

        var offerJson = JsonSerializer.Serialize(new
        {
            offerId            = command.OfferRefId,
            sessionId          = offerDetail.SessionId,
            inventoryId        = offerDetail.InventoryId,
            flightNumber       = offerDetail.FlightNumber,
            departureDate      = offerDetail.DepartureDate,
            departureTime      = offerDetail.DepartureTime,
            arrivalTime        = offerDetail.ArrivalTime,
            origin             = offerDetail.Origin,
            destination        = offerDetail.Destination,
            aircraftType       = offerDetail.AircraftType,
            offerExpiresAt     = offerDetail.ExpiresAt,
            cabinCode          = offerItem?.CabinCode,
            fareBasisCode      = offerItem?.FareBasisCode,
            fareFamily         = offerItem?.FareFamily,
            unitAmount         = offerItem?.TotalAmount ?? 0m,
            unitBaseFareAmount = offerItem?.BaseFareAmount ?? 0m,
            unitTaxAmount      = offerItem?.TaxAmount ?? 0m,
            totalAmount        = (offerItem?.TotalAmount ?? 0m) * paxCount,
            baseFareAmount     = (offerItem?.BaseFareAmount ?? 0m) * paxCount,
            taxAmount          = (offerItem?.TaxAmount ?? 0m) * paxCount,
            isRefundable       = offerItem?.IsRefundable ?? false,
            isChangeable       = offerItem?.IsChangeable ?? false,
            passengerCount     = paxCount
        }, JsonOpts);

        await _orderServiceClient.AddOfferAsync(basket.BasketId, offerJson, cancellationToken);

        // 5. Update named passengers on the basket.
        //    Body is a JSON array stored verbatim as BasketData.passengers by the Order MS.
        //    PassengerId is set to the NDC PaxID so e-tickets reference it consistently.
        var passengersPayload = command.Passengers.Select(p => BuildPassengerNode(p)).ToList();
        var passengersJson = JsonSerializer.Serialize(passengersPayload, JsonOpts);

        await _orderServiceClient.UpdatePassengersAsync(basket.BasketId, passengersJson, cancellationToken);

        // 6. Confirm the basket — processes payment, issues e-tickets, writes manifest.
        var (cardNumber, expiryDate, cvv, cardholderName, paymentMethod) = MapPayment(command.PaymentCard);

        var confirmCommand = new ConfirmBasketCommand(
            BasketId              : basket.BasketId,
            ChannelCode           : "NDC",
            PaymentMethod         : paymentMethod,
            CardNumber            : cardNumber,
            ExpiryDate            : expiryDate,
            Cvv                   : cvv,
            CardholderName        : cardholderName,
            LoyaltyPointsToRedeem : null);

        var order = await _confirmBasketHandler.HandleAsync(confirmCommand, cancellationToken);

        _logger.LogInformation(
            "[NDC] OrderCreate confirmed: BookingReference={BookingRef} OrderId={OrderId}",
            order.BookingReference, order.OrderId);

        return new NdcOrderCreateResult(NdcOrderCreateOutcome.Success, order, offerDetail);
    }

    private static object BuildPassengerNode(NdcOrderCreatePassenger p)
    {
        if (p.Email is not null || p.Phone is not null)
        {
            return new
            {
                passengerId = p.PaxId,
                type        = p.Ptc,
                givenName   = p.GivenName,
                surname     = p.Surname,
                dob         = p.Dob,
                gender      = p.GenderCode,
                contacts    = new { email = p.Email, phone = p.Phone },
                docs        = Array.Empty<object>()
            };
        }

        return new
        {
            passengerId = p.PaxId,
            type        = p.Ptc,
            givenName   = p.GivenName,
            surname     = p.Surname,
            dob         = p.Dob,
            gender      = p.GenderCode,
            docs        = Array.Empty<object>()
        };
    }

    private static (string? CardNumber, string? ExpiryDate, string? Cvv, string? CardholderName, string PaymentMethod)
        MapPayment(NdcOrderCreatePaymentCard? card)
    {
        if (card is null)
            return (null, null, null, null, "CreditCard");

        // ConfirmBasketHandler expects expiry as "MM/YYYY"
        var expiryDate = $"{card.ExpiryMonth.PadLeft(2, '0')}/{card.ExpiryYear}";

        return (card.CardNumber, expiryDate, card.Cvv, card.CardholderName, "CreditCard");
    }
}

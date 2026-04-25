using System.Text.Json;
using Microsoft.Extensions.Logging;
using ReservationSystem.Orchestration.Retail.Application.ConfirmBasket;
using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;

namespace ReservationSystem.Orchestration.Retail.Application.CheckInAncillaries;

public sealed record CheckInDocument(string DocumentNumber, string DocumentType, string PassengerId, string SegmentRef, decimal Amount, string Currency);
public sealed record CheckInAncillariesResult(bool Success, string PaymentReference, IReadOnlyList<CheckInDocument> Documents);

public sealed class CheckInAncillariesHandler
{
    private readonly OrderServiceClient _orderServiceClient;
    private readonly PaymentServiceClient _paymentServiceClient;
    private readonly DeliveryServiceClient _deliveryServiceClient;
    private readonly ILogger<CheckInAncillariesHandler> _logger;

    public CheckInAncillariesHandler(
        OrderServiceClient orderServiceClient,
        PaymentServiceClient paymentServiceClient,
        DeliveryServiceClient deliveryServiceClient,
        ILogger<CheckInAncillariesHandler> logger)
    {
        _orderServiceClient = orderServiceClient;
        _paymentServiceClient = paymentServiceClient;
        _deliveryServiceClient = deliveryServiceClient;
        _logger = logger;
    }

    public async Task<CheckInAncillariesResult> HandleAsync(CheckInAncillariesCommand command, CancellationToken ct)
    {
        // ── Determine authoritative amounts from the basket when available ────────
        var seatTotal = 0m;
        var bagTotal  = 0m;

        if (command.BasketId.HasValue)
        {
            var basket = await _orderServiceClient.GetBasketAsync(command.BasketId.Value, ct);
            if (basket?.BasketData.HasValue == true)
            {
                var basketJson = basket.BasketData.Value.GetRawText();
                seatTotal = SumBasketProperty(basketJson, "seats",  "price");
                bagTotal  = SumBasketProperty(basketJson, "bags",   "price");
            }
        }

        // Fall back to request body amounts if basket not loaded
        if (seatTotal == 0m && bagTotal == 0m)
        {
            seatTotal = command.SeatSelections.Sum(s => s.SeatPrice);
            bagTotal  = command.BagSelections.Sum(b => b.Price);
        }

        var total    = seatTotal + bagTotal;
        var currency = command.SeatSelections.FirstOrDefault()?.Currency
                    ?? command.BagSelections.FirstOrDefault()?.Currency
                    ?? "GBP";

        if (total <= 0)
            throw new InvalidOperationException("No ancillary charges to process.");

        // ── Initialise payment ────────────────────────────────────────────────────
        var paymentId = await _paymentServiceClient.InitialiseAsync(
            "CC", currency, total,
            $"Check-in ancillaries — {command.BookingReference}", ct);

        try
        {
            if (seatTotal > 0)
            {
                await _paymentServiceClient.AuthoriseAsync(paymentId, "Seat", seatTotal,
                    command.CardNumber, command.ExpiryDate, command.Cvv, command.CardholderName, ct);
                await _paymentServiceClient.SettleAsync(paymentId, seatTotal, ct);
            }

            if (bagTotal > 0)
            {
                await _paymentServiceClient.AuthoriseAsync(paymentId, "Bag", bagTotal,
                    command.CardNumber, command.ExpiryDate, command.Cvv, command.CardholderName, ct);
                await _paymentServiceClient.SettleAsync(paymentId, bagTotal, ct);
            }
        }
        catch
        {
            try { await _paymentServiceClient.VoidAsync(paymentId, "AncillaryPaymentFailure", ct); } catch { }
            throw;
        }

        // ── Link booking reference to the payment record ─────────────────────────
        var settledAt = DateTime.UtcNow;
        try
        {
            await _paymentServiceClient.UpdateBookingReferenceAsync(paymentId, command.BookingReference, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CheckInAncillaries] Booking reference update failed for {BookingReference}", command.BookingReference);
        }

        // ── Record payment in the order ───────────────────────────────────────────
        try
        {
            await _orderServiceClient.UpdateOrderPaymentsAsync(
                command.BookingReference,
                new[]
                {
                    new
                    {
                        paymentReference = paymentId,
                        description      = $"Check-in ancillaries — {command.BookingReference}",
                        method           = "CreditCard",
                        cardLast4        = command.CardLast4 ?? "",
                        cardType         = command.CardType  ?? "",
                        authorisedAmount = total,
                        settledAmount    = total,
                        currency,
                        status           = "Settled",
                        authorisedAt     = settledAt.ToString("O"),
                        settledAt        = settledAt.ToString("O")
                    }
                },
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CheckInAncillaries] Payment order update failed for {BookingReference}", command.BookingReference);
        }

        // ── Persist selections to the order ──────────────────────────────────────
        if (command.SeatSelections.Count > 0)
        {
            try
            {
                await _orderServiceClient.UpdateOrderSeatsPostSaleAsync(
                    command.BookingReference,
                    command.SeatSelections.Select(s => new
                    {
                        passengerId = s.PassengerId,
                        segmentId   = s.SegmentId,
                        seatNumber  = s.SeatNumber,
                        price       = s.SeatPrice,
                        currency    = s.Currency
                    }),
                    ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CheckInAncillaries] Seat order update failed for {BookingReference}", command.BookingReference);
            }
        }

        if (command.BagSelections.Count > 0)
        {
            try
            {
                await _orderServiceClient.UpdateOrderBagsPostSaleAsync(
                    command.BookingReference,
                    command.BagSelections.Select(b => new
                    {
                        passengerId    = b.PassengerId,
                        segmentId      = b.SegmentId,
                        additionalBags = b.AdditionalBags,
                        bagOfferId     = b.BagOfferId,
                        price          = b.Price,
                        currency       = b.Currency
                    }),
                    ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CheckInAncillaries] Bag order update failed for {BookingReference}", command.BookingReference);
            }
        }

        // ── Issue EMDs and collect document numbers ───────────────────────────────
        var documents = new List<CheckInDocument>();

        foreach (var seat in command.SeatSelections.Where(s => s.SeatPrice > 0))
        {
            try
            {
                var docNumber = await _deliveryServiceClient.IssueDocumentAsync(
                    command.BookingReference, "SeatAncillary",
                    seat.PassengerId, seat.SegmentId,
                    seat.SeatPrice, seat.Currency, paymentId, ct);

                if (!string.IsNullOrEmpty(docNumber))
                    documents.Add(new CheckInDocument(docNumber, "SeatAncillary", seat.PassengerId, seat.SegmentId, seat.SeatPrice, seat.Currency));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CheckInAncillaries] Seat EMD failed for {PassengerId}", seat.PassengerId);
            }
        }

        foreach (var bag in command.BagSelections.Where(b => b.Price > 0))
        {
            try
            {
                var docNumber = await _deliveryServiceClient.IssueDocumentAsync(
                    command.BookingReference, "BagAncillary",
                    bag.PassengerId, bag.SegmentId,
                    bag.Price, bag.Currency, paymentId, ct);

                if (!string.IsNullOrEmpty(docNumber))
                    documents.Add(new CheckInDocument(docNumber, "BagAncillary", bag.PassengerId, bag.SegmentId, bag.Price, bag.Currency));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CheckInAncillaries] Bag EMD failed for {PassengerId}", bag.PassengerId);
            }
        }

        return new CheckInAncillariesResult(true, paymentId, documents);
    }

    private static decimal SumBasketProperty(string basketJson, string arrayKey, string amountKey)
    {
        try
        {
            using var doc = JsonDocument.Parse(basketJson);
            if (!doc.RootElement.TryGetProperty(arrayKey, out var arr) || arr.ValueKind != JsonValueKind.Array)
                return 0m;
            return arr.EnumerateArray()
                .Where(el => el.TryGetProperty(amountKey, out var v) && v.ValueKind == JsonValueKind.Number)
                .Sum(el => el.GetProperty(amountKey).GetDecimal());
        }
        catch { return 0m; }
    }
}

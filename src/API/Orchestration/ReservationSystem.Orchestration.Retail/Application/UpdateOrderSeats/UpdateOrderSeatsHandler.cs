using ReservationSystem.Orchestration.Retail.Application.ConfirmBasket;
using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;

namespace ReservationSystem.Orchestration.Retail.Application.UpdateOrderSeats;

/// <summary>
/// Orchestrates post-sale seat purchase on a confirmed order.
///
/// For free seat changes (no SeatOfferId supplied): updates seat assignments in Order MS only.
///
/// For paid seat purchases:
/// 1. Validate each seat offer via Ancillary MS — resolve authoritative price and tax.
/// 2. Authorise payment via Payment MS (type=SeatAncillary).
/// 3. Hold then sell the seat(s) in Offer MS inventory.
/// 4. Settle payment.
/// 5. Update seat assignments in Order MS.
/// 6. Issue a SeatAncillary document (EMD) per seat in Delivery MS.
/// </summary>
public sealed class UpdateOrderSeatsHandler
{
    private readonly OrderServiceClient _orderServiceClient;
    private readonly SeatServiceClient _seatServiceClient;
    private readonly OfferServiceClient _offerServiceClient;
    private readonly PaymentServiceClient _paymentServiceClient;
    private readonly DeliveryServiceClient _deliveryServiceClient;

    public UpdateOrderSeatsHandler(
        OrderServiceClient orderServiceClient,
        SeatServiceClient seatServiceClient,
        OfferServiceClient offerServiceClient,
        PaymentServiceClient paymentServiceClient,
        DeliveryServiceClient deliveryServiceClient)
    {
        _orderServiceClient = orderServiceClient;
        _seatServiceClient = seatServiceClient;
        _offerServiceClient = offerServiceClient;
        _paymentServiceClient = paymentServiceClient;
        _deliveryServiceClient = deliveryServiceClient;
    }

    public async Task<UpdateOrderSeatsResponse> HandleAsync(
        string bookingReference,
        UpdateOrderSeatsCommand command,
        CancellationToken ct)
    {
        // 1. Validate order exists and is mutable
        var order = await _orderServiceClient.GetOrderByRefAsync(bookingReference, ct)
            ?? throw new KeyNotFoundException($"Order '{bookingReference}' not found.");

        if (order.OrderStatus != "Confirmed" && order.OrderStatus != "Changed")
            throw new InvalidOperationException($"Order is not mutable. Status: {order.OrderStatus}");

        var paidSelections = command.SeatSelections
            .Where(s => !string.IsNullOrEmpty(s.SeatOfferId))
            .ToList();

        // Free-only seat change — update Order MS only, no payment required
        if (paidSelections.Count == 0)
        {
            var freePayload = command.SeatSelections.Select(s => new
            {
                passengerId = s.PassengerId,
                segmentId = s.SegmentId,
                seatNumber = s.SeatNumber,
                price = s.Price,
                tax = s.Tax,
                currency = s.Currency
            }).ToList();
            await _orderServiceClient.UpdateOrderSeatsPostSaleAsync(bookingReference, freePayload, ct);
            return new UpdateOrderSeatsResponse { BookingReference = bookingReference, Updated = true };
        }

        // 2. Validate each paid seat offer — resolve authoritative price and tax
        var currency = order.CurrencyCode;
        decimal totalSeatAmount = 0m;
        var validatedSeats = new List<ValidatedSeatItem>();

        foreach (var sel in paidSelections)
        {
            var offer = await _seatServiceClient.GetSeatOfferByIdAsync(sel.SeatOfferId, ct);
            if (offer is null || !offer.IsSelectable)
                throw new InvalidOperationException($"Seat offer '{sel.SeatOfferId}' is not valid or no longer available.");
            if (!offer.IsChargeable)
                throw new InvalidOperationException($"Seat offer '{sel.SeatOfferId}' is not a chargeable offer.");

            validatedSeats.Add(new ValidatedSeatItem
            {
                PassengerId = sel.PassengerId,
                SegmentId = sel.SegmentId,
                SeatNumber = sel.SeatNumber,
                InventoryId = sel.InventoryId,
                CabinCode = sel.CabinCode,
                Price = offer.Price,
                Tax = offer.Tax,
                Currency = offer.CurrencyCode
            });
            totalSeatAmount += offer.Price + offer.Tax;
        }

        // 3. Initialise and authorise payment
        var paymentId = await _paymentServiceClient.InitialiseAsync(
            paymentType: "SeatAncillary",
            method: command.Payment.Method,
            currencyCode: currency,
            amount: totalSeatAmount,
            description: $"Seat ancillary — {bookingReference}",
            ct);

        try
        {
            await _paymentServiceClient.AuthoriseAsync(
                paymentId, totalSeatAmount,
                command.Payment.CardNumber,
                command.Payment.ExpiryDate,
                command.Payment.Cvv,
                command.Payment.CardholderName,
                ct);
        }
        catch
        {
            await _paymentServiceClient.VoidAsync(paymentId, "PaymentAuthorisationFailure", ct);
            throw;
        }

        // 4. Hold then sell each seat in inventory — removes seat from availability
        try
        {
            foreach (var grp in validatedSeats.GroupBy(s => (s.InventoryId, s.CabinCode)))
            {
                var passengers = grp
                    .Select(s => ((string?)s.SeatNumber, (string?)s.PassengerId))
                    .ToList();
                await _offerServiceClient.HoldInventoryAsync(
                    grp.Key.InventoryId, grp.Key.CabinCode, passengers, order.OrderId, cancellationToken: ct);
            }

            var sellItems = validatedSeats
                .GroupBy(s => (s.InventoryId, s.CabinCode))
                .Select(g => (g.Key.InventoryId, g.Key.CabinCode))
                .ToList<(Guid, string)>();
            await _offerServiceClient.SellInventoryAsync(order.OrderId, sellItems, ct);
        }
        catch
        {
            await _paymentServiceClient.VoidAsync(paymentId, "InventoryUnavailable", ct);
            throw;
        }

        // 5. Settle payment — capture funds
        await _paymentServiceClient.SettleAsync(paymentId, totalSeatAmount, ct);

        // 6. Update seat assignments in Order MS (paid and any free selections together)
        var seatsPayload = command.SeatSelections.Select(s =>
        {
            var isPaid = !string.IsNullOrEmpty(s.SeatOfferId);
            var validated = isPaid
                ? validatedSeats.Find(v => v.PassengerId == s.PassengerId && v.SegmentId == s.SegmentId)
                : null;
            return new
            {
                passengerId = s.PassengerId,
                segmentId = s.SegmentId,
                seatNumber = s.SeatNumber,
                seatOfferId = isPaid ? s.SeatOfferId : null,
                paymentReference = isPaid ? paymentId : (string?)null,
                price = validated?.Price ?? s.Price,
                tax = validated?.Tax ?? s.Tax,
                currency = validated?.Currency ?? s.Currency
            };
        }).ToList();

        await _orderServiceClient.UpdateOrderSeatsPostSaleAsync(bookingReference, seatsPayload, ct);

        // 7. Issue SeatAncillary document (EMD) per paid seat — non-fatal
        foreach (var seat in validatedSeats)
        {
            try
            {
                await _deliveryServiceClient.IssueDocumentAsync(
                    bookingReference, "SeatAncillary",
                    seat.PassengerId, seat.SegmentId,
                    seat.Price + seat.Tax, seat.Currency, paymentId, ct);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[UpdateOrderSeats] Document issuance failed for {seat.PassengerId}: {ex.Message}");
            }
        }

        return new UpdateOrderSeatsResponse
        {
            BookingReference = bookingReference,
            Updated = true,
            TotalSeatAmount = totalSeatAmount,
            PaymentId = paymentId
        };
    }

    private sealed class ValidatedSeatItem
    {
        public string PassengerId { get; init; } = string.Empty;
        public string SegmentId { get; init; } = string.Empty;
        public string SeatNumber { get; init; } = string.Empty;
        public Guid InventoryId { get; init; }
        public string CabinCode { get; init; } = string.Empty;
        public decimal Price { get; init; }
        public decimal Tax { get; init; }
        public string Currency { get; init; } = string.Empty;
    }
}

public sealed class UpdateOrderSeatsCommand
{
    public List<SeatSelectionItem> SeatSelections { get; init; } = [];
    public PaymentDetails Payment { get; init; } = new();
}

public sealed class SeatSelectionItem
{
    public string PassengerId { get; init; } = string.Empty;
    public string SegmentId { get; init; } = string.Empty;
    public string SeatNumber { get; init; } = string.Empty;
    public string SeatOfferId { get; init; } = string.Empty;
    public Guid InventoryId { get; init; }
    public string CabinCode { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public decimal Tax { get; init; }
    public string Currency { get; init; } = string.Empty;
}

public sealed class PaymentDetails
{
    public string Method { get; init; } = "CreditCard";
    public string CardNumber { get; init; } = string.Empty;
    public string ExpiryDate { get; init; } = string.Empty;
    public string Cvv { get; init; } = string.Empty;
    public string CardholderName { get; init; } = string.Empty;
}

public sealed class UpdateOrderSeatsResponse
{
    public string BookingReference { get; init; } = string.Empty;
    public bool Updated { get; init; }
    public decimal TotalSeatAmount { get; init; }
    public string? PaymentId { get; init; }
}

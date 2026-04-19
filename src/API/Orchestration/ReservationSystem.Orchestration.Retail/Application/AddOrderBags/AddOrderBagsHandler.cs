using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;

namespace ReservationSystem.Orchestration.Retail.Application.AddOrderBags;

/// <summary>
/// Orchestrates post-sale bag purchase across Bag, Payment, Order and Delivery microservices.
///
/// Sequence:
/// 1. Validate each bag offer via Bag MS — resolve price and verify still active.
/// 2. Authorise payment via Payment MS (description=BagAncillary).
/// 3. Settle payment.
/// 4. Update order bags in Order MS.
/// 5. Issue BagAncillary document per bag selection in Delivery MS.
/// </summary>
public sealed class AddOrderBagsHandler
{
    private readonly OrderServiceClient _orderServiceClient;
    private readonly BagServiceClient _bagServiceClient;
    private readonly PaymentServiceClient _paymentServiceClient;
    private readonly DeliveryServiceClient _deliveryServiceClient;

    public AddOrderBagsHandler(
        OrderServiceClient orderServiceClient,
        BagServiceClient bagServiceClient,
        PaymentServiceClient paymentServiceClient,
        DeliveryServiceClient deliveryServiceClient)
    {
        _orderServiceClient = orderServiceClient;
        _bagServiceClient = bagServiceClient;
        _paymentServiceClient = paymentServiceClient;
        _deliveryServiceClient = deliveryServiceClient;
    }

    public async Task<AddOrderBagsResponse> HandleAsync(
        string bookingReference,
        AddOrderBagsCommand command,
        CancellationToken ct)
    {
        // 1. Validate order exists and is mutable
        var order = await _orderServiceClient.GetOrderByRefAsync(bookingReference, ct)
            ?? throw new KeyNotFoundException($"Order '{bookingReference}' not found.");

        if (order.OrderStatus != "Confirmed" && order.OrderStatus != "Changed")
            throw new InvalidOperationException($"Order is not mutable. Status: {order.OrderStatus}");

        var currency = order.CurrencyCode;

        // 2. Validate each bag offer and compute total
        decimal totalBagAmount = 0m;
        var validatedBags = new List<ValidatedBagItem>();

        foreach (var sel in command.BagSelections)
        {
            var offer = await _bagServiceClient.GetBagOfferAsync(sel.BagOfferId, ct);
            if (offer is null || !offer.IsValid)
                throw new InvalidOperationException($"Bag offer '{sel.BagOfferId}' is not valid or has expired.");

            validatedBags.Add(new ValidatedBagItem
            {
                PassengerRef = sel.PassengerRef,
                InventoryId = sel.InventoryId,
                BagOfferId = sel.BagOfferId,
                BagSequence = offer.BagSequence,
                Price = offer.Price,
                Tax = offer.Tax,
                Currency = offer.CurrencyCode
            });
            totalBagAmount += offer.Price + offer.Tax;
        }

        // 3. Authorise payment
        var paymentId = await _paymentServiceClient.InitialiseAsync(
            method: command.Payment.Method,
            currencyCode: currency,
            amount: totalBagAmount,
            description: $"Bag ancillary — {bookingReference}",
            ct);

        try
        {
            await _paymentServiceClient.AuthoriseAsync(
                paymentId, "Bag", totalBagAmount,
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

        // 4. Settle payment
        await _paymentServiceClient.SettleAsync(paymentId, totalBagAmount, ct);
        try { await _paymentServiceClient.UpdateBookingReferenceAsync(paymentId, bookingReference, ct); } catch { }

        // 5. Update order bags in Order MS
        var bagsPayload = validatedBags.Select(b => new
        {
            passengerId = b.PassengerRef,
            segmentId = b.InventoryId,
            bagOfferId = b.BagOfferId,
            additionalBags = b.BagSequence,
            price = b.Price,
            tax = b.Tax,
            currency = b.Currency,
            paymentReference = paymentId
        }).ToList();

        await _orderServiceClient.UpdateOrderBagsPostSaleAsync(bookingReference, bagsPayload, ct);

        // 6. Issue BagAncillary document per bag selection
        foreach (var bag in validatedBags)
        {
            try
            {
                await _deliveryServiceClient.IssueDocumentAsync(
                    bookingReference, "BagAncillary",
                    bag.PassengerRef, bag.InventoryId,
                    bag.Price, bag.Currency, paymentId, ct);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[AddOrderBags] Document issuance failed for {bag.PassengerRef}: {ex.Message}");
            }
        }

        return new AddOrderBagsResponse
        {
            BookingReference = bookingReference,
            TotalBagAmount = totalBagAmount,
            PaymentId = paymentId
        };
    }

    private sealed class ValidatedBagItem
    {
        public string PassengerRef { get; init; } = string.Empty;
        public string InventoryId { get; init; } = string.Empty;
        public string BagOfferId { get; init; } = string.Empty;
        public int BagSequence { get; init; }
        public decimal Price { get; init; }
        public decimal Tax { get; init; }
        public string Currency { get; init; } = string.Empty;
    }
}

public sealed class AddOrderBagsCommand
{
    public List<BagSelectionItem> BagSelections { get; init; } = [];
    public PaymentDetails Payment { get; init; } = new();
}

public sealed class BagSelectionItem
{
    public string BagOfferId { get; init; } = string.Empty;
    public string PassengerRef { get; init; } = string.Empty;
    public string InventoryId { get; init; } = string.Empty;
}

public sealed class PaymentDetails
{
    public string Method { get; init; } = "CreditCard";
    public string CardNumber { get; init; } = string.Empty;
    public string ExpiryDate { get; init; } = string.Empty;
    public string Cvv { get; init; } = string.Empty;
    public string CardholderName { get; init; } = string.Empty;
}

public sealed class AddOrderBagsResponse
{
    public string BookingReference { get; init; } = string.Empty;
    public decimal TotalBagAmount { get; init; }
    public string PaymentId { get; init; } = string.Empty;
}

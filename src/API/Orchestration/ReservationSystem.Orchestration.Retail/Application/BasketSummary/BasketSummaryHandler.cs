using System.Text.Json;
using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices.Dto;
using ReservationSystem.Orchestration.Retail.Models.Responses;

namespace ReservationSystem.Orchestration.Retail.Application.BasketSummary;

public sealed record BasketSummaryQuery(Guid BasketId);

public sealed class BasketSummaryHandler
{
    private readonly OrderServiceClient _orderServiceClient;
    private readonly OfferServiceClient _offerServiceClient;

    public BasketSummaryHandler(OrderServiceClient orderServiceClient, OfferServiceClient offerServiceClient)
    {
        _orderServiceClient = orderServiceClient;
        _offerServiceClient = offerServiceClient;
    }

    public async Task<BasketSummaryResponse?> HandleAsync(BasketSummaryQuery query, CancellationToken cancellationToken)
    {
        // 1. Retrieve the basket
        var basket = await _orderServiceClient.GetBasketAsync(query.BasketId, cancellationToken);
        if (basket is null) return null;

        var basketDataJson = basket.BasketData.HasValue ? basket.BasketData.Value.GetRawText() : null;

        // 2. Parse offer refs (offerId + sessionId) from each flightOffer in the basket
        var offerRefs = ParseOfferRefs(basketDataJson);

        // 3. For each offer: call reprice on the Offer MS, which adds tax lines and sets validated = true
        var repricedByOfferId = new Dictionary<Guid, RepriceOfferDto>();
        foreach (var (offerId, sessionId) in offerRefs)
        {
            var repriced = await _offerServiceClient.RepriceOfferAsync(offerId, sessionId, cancellationToken);
            if (repriced is not null)
                repricedByOfferId[offerId] = repriced;
        }

        // 4. Build the summary flights from basket data + reprice results
        var flights = BuildSummaryFlights(basketDataJson, repricedByOfferId);

        return new BasketSummaryResponse
        {
            BasketId        = basket.BasketId,
            Status          = basket.BasketStatus,
            Currency        = basket.CurrencyCode,
            ExpiresAt       = basket.ExpiresAt.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            Flights         = flights,
            TotalFareAmount = basket.TotalFareAmount ?? 0m,
            TotalSeatAmount = basket.TotalSeatAmount,
            TotalBagAmount  = basket.TotalBagAmount,
            TotalPrice      = basket.TotalAmount ?? 0m
        };
    }

    private static List<(Guid OfferId, Guid? SessionId)> ParseOfferRefs(string? basketDataJson)
    {
        var refs = new List<(Guid, Guid?)>();
        if (basketDataJson is null) return refs;

        try
        {
            using var doc = JsonDocument.Parse(basketDataJson);
            var root = doc.RootElement;

            if (root.TryGetProperty("flightOffers", out var offersEl) &&
                offersEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var offer in offersEl.EnumerateArray())
                {
                    if (offer.TryGetProperty("offerId", out var offerIdEl) &&
                        offerIdEl.TryGetGuid(out var offerId))
                    {
                        Guid? sessionId = null;
                        if (offer.TryGetProperty("sessionId", out var sessionIdEl) &&
                            sessionIdEl.TryGetGuid(out var sid))
                            sessionId = sid;

                        refs.Add((offerId, sessionId));
                    }
                }
            }
        }
        catch { /* Return whatever was parsed */ }

        return refs;
    }

    private static List<SummaryFlight> BuildSummaryFlights(
        string? basketDataJson,
        Dictionary<Guid, RepriceOfferDto> repricedByOfferId)
    {
        var flights = new List<SummaryFlight>();
        if (basketDataJson is null) return flights;

        try
        {
            using var doc = JsonDocument.Parse(basketDataJson);
            var root = doc.RootElement;

            if (!root.TryGetProperty("flightOffers", out var offersEl) ||
                offersEl.ValueKind != JsonValueKind.Array)
                return flights;

            foreach (var offer in offersEl.EnumerateArray())
            {
                if (!offer.TryGetProperty("offerId", out var offerIdEl) ||
                    !offerIdEl.TryGetGuid(out var offerId))
                    continue;

                Guid? sessionId = null;
                if (offer.TryGetProperty("sessionId", out var sessionIdEl) &&
                    sessionIdEl.TryGetGuid(out var sid))
                    sessionId = sid;

                Guid? inventoryId = null;
                if (offer.TryGetProperty("inventoryId", out var invEl) &&
                    invEl.TryGetGuid(out var inv))
                    inventoryId = inv;

                // Pricing comes from the basket data (locked at basket creation)
                var baseFareAmount = offer.TryGetProperty("unitBaseFareAmount", out var bf) ? bf.GetDecimal() : 0m;
                var taxAmount      = offer.TryGetProperty("unitTaxAmount",      out var ta) ? ta.GetDecimal() : 0m;
                var totalAmount    = offer.TryGetProperty("unitAmount",          out var tot) ? tot.GetDecimal() : 0m;

                // Tax lines + validated come from the reprice result
                IReadOnlyList<SummaryTaxLine>? taxLines = null;
                var validated = false;
                if (repricedByOfferId.TryGetValue(offerId, out var repriced))
                {
                    validated = repriced.Validated;
                    var repricedItem = repriced.Offers.FirstOrDefault(o => o.OfferId == offerId)
                        ?? repriced.Offers.FirstOrDefault();
                    if (repricedItem?.TaxLines is not null)
                    {
                        taxLines = repricedItem.TaxLines
                            .Select(t => new SummaryTaxLine { Code = t.Code, Amount = t.Amount, Description = t.Description })
                            .ToList();
                    }
                }

                flights.Add(new SummaryFlight
                {
                    OfferId        = offerId,
                    SessionId      = sessionId,
                    InventoryId    = inventoryId,
                    FlightNumber   = offer.TryGetProperty("flightNumber",  out var fn)  ? fn.GetString()  ?? "" : "",
                    Origin         = offer.TryGetProperty("origin",        out var or)  ? or.GetString()  ?? "" : "",
                    Destination    = offer.TryGetProperty("destination",   out var dst) ? dst.GetString() ?? "" : "",
                    DepartureDate  = offer.TryGetProperty("departureDate", out var dd)  ? dd.GetString()  ?? "" : "",
                    DepartureTime  = offer.TryGetProperty("departureTime", out var dt)  ? dt.GetString()  ?? "" : "",
                    ArrivalTime    = offer.TryGetProperty("arrivalTime",   out var at)  ? at.GetString()  ?? "" : "",
                    CabinCode      = offer.TryGetProperty("cabinCode",     out var cc)  ? cc.GetString()  ?? "" : "",
                    FareFamily     = offer.TryGetProperty("fareFamily",    out var ff)  ? ff.GetString()       : null,
                    Validated      = validated,
                    BaseFareAmount = baseFareAmount,
                    TaxAmount      = taxAmount,
                    TotalAmount    = totalAmount,
                    TaxLines       = taxLines
                });
            }
        }
        catch { /* Return whatever was parsed */ }

        return flights;
    }
}

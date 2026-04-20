using System.Text.Json;
using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Retail.Models.Responses;

namespace ReservationSystem.Orchestration.Retail.Application.PaymentSummary;

public sealed record PaymentSummaryQuery(Guid BasketId);

/// <summary>
/// Builds a complete payment-screen summary from the persisted basket.
/// All calculations (totals, taxes, per-flight amounts) are performed here;
/// the Angular client renders the result as-is with no business logic.
/// </summary>
public sealed class PaymentSummaryHandler
{
    private readonly OrderServiceClient _orderServiceClient;

    private static readonly IReadOnlyDictionary<string, string> CabinNames =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["F"] = "First Class",
            ["J"] = "Business Class",
            ["W"] = "Premium Economy",
            ["Y"] = "Economy"
        };

    public PaymentSummaryHandler(OrderServiceClient orderServiceClient)
    {
        _orderServiceClient = orderServiceClient;
    }

    public async Task<PaymentSummaryResponse?> HandleAsync(PaymentSummaryQuery query, CancellationToken cancellationToken)
    {
        var basket = await _orderServiceClient.GetBasketAsync(query.BasketId, cancellationToken);
        if (basket is null) return null;

        var basketDataJson = basket.BasketData.HasValue ? basket.BasketData.Value.GetRawText() : null;

        // ── Parse basket data ──────────────────────────────────────────────────
        using var doc = basketDataJson is not null
            ? JsonDocument.Parse(basketDataJson)
            : JsonDocument.Parse("{}");

        var root = doc.RootElement;

        var bookingType = root.TryGetProperty("bookingType", out var btEl) ? btEl.GetString() ?? "Revenue" : "Revenue";
        var ticketingTimeLimit = root.TryGetProperty("ticketingTimeLimit", out var ttlEl) ? ttlEl.GetString() : null;

        // ── Flights ────────────────────────────────────────────────────────────
        var flights = ParseFlights(root);

        // Build a lookup: basketItemId → flightNumber (for seat/bag cross-referencing)
        var flightNumberByItemId = flights
            .Where(f => f.BasketItemId is not null)
            .ToDictionary(f => f.BasketItemId!, f => f.FlightNumber, StringComparer.OrdinalIgnoreCase);

        // ── Passengers ────────────────────────────────────────────────────────
        var passengers = ParsePassengers(root);

        // ── Seat selections ───────────────────────────────────────────────────
        var seatSelections = ParseSeatSelections(root, flightNumberByItemId);

        // ── Bag selections ────────────────────────────────────────────────────
        var bagSelections = ParseBagSelections(root, flightNumberByItemId);

        // ── Product selections ────────────────────────────────────────────────
        var productSelections = ParseProductSelections(root);

        // ── SSR selections ────────────────────────────────────────────────────
        var ssrSelections = ParseSsrSelections(root);

        // ── Totals ─────────────────────────────────────────────────────────────
        var isReward = string.Equals(bookingType, "Reward", StringComparison.OrdinalIgnoreCase);

        var totalFareSum = flights.Sum(f => f.TotalAmount);
        var taxSum       = flights.Sum(f => f.TaxAmount);

        // All prices are tax-inclusive; taxAmount is informational for accounting only, not additive.
        // Revenue: fareAmount is the full tax-inclusive total. Reward: fare paid in points, cash = taxes only.
        var fareAmount = isReward ? 0m : totalFareSum;
        var taxAmount  = isReward ? (basket.TotalFareAmount ?? 0m) : taxSum;
        var seatAmount    = seatSelections.Sum(s => s.Price);
        var bagAmount     = bagSelections.Sum(b => b.Price);

        var productAmount = productSelections.Sum(p => p.Price);
        var grandTotal    = fareAmount + seatAmount + bagAmount + productAmount;

        var pointsAmount = 0;
        if (isReward && root.TryGetProperty("pointsPrice", out var ppEl))
            pointsAmount = ppEl.TryGetInt32(out var pts) ? pts : 0;
        // Fall back to summing per-flight points totals
        if (isReward && pointsAmount == 0)
            pointsAmount = flights.Sum(f => f.PointsPrice);

        var summaryFlights = flights.Select(f => new PaymentSummaryFlight
        {
            OfferId          = f.OfferId,
            FlightNumber     = f.FlightNumber,
            Origin           = f.Origin,
            Destination      = f.Destination,
            DepartureDateTime = f.DepartureDateTime,
            ArrivalDateTime  = f.ArrivalDateTime,
            CabinCode        = f.CabinCode,
            CabinName        = CabinNames.TryGetValue(f.CabinCode, out var name) ? name : f.CabinCode,
            FareFamily       = f.FareFamily,
            FareAmount       = isReward ? 0m : f.BaseFareAmount,
            TaxAmount        = f.TaxAmount,
            TotalAmount      = isReward ? f.TaxAmount : f.TotalAmount
        }).ToList();

        return new PaymentSummaryResponse
        {
            BasketId           = basket.BasketId,
            BookingType        = bookingType,
            Currency           = basket.CurrencyCode,
            TicketingTimeLimit = ticketingTimeLimit,
            Flights            = summaryFlights,
            Passengers         = passengers,
            SeatSelections     = seatSelections,
            BagSelections      = bagSelections,
            ProductSelections  = productSelections,
            SsrSelections      = ssrSelections,
            Totals = new PaymentSummaryTotals
            {
                FareAmount    = fareAmount,
                TaxAmount     = taxAmount,
                SeatAmount    = seatAmount,
                BagAmount     = bagAmount,
                ProductAmount = productAmount,
                PointsAmount  = pointsAmount,
                GrandTotal    = grandTotal
            }
        };
    }

    // ── Parsers ────────────────────────────────────────────────────────────────

    private static List<FlightOfferData> ParseFlights(JsonElement root)
    {
        var flights = new List<FlightOfferData>();

        if (!root.TryGetProperty("flightOffers", out var offersEl) ||
            offersEl.ValueKind != JsonValueKind.Array)
            return flights;

        foreach (var offer in offersEl.EnumerateArray())
        {
            var offerId      = offer.TryGetProperty("offerId",      out var oid) && oid.TryGetGuid(out var g) ? g : Guid.Empty;
            var basketItemId = offer.TryGetProperty("basketItemId", out var bid) ? bid.GetString() : null;
            var flightNumber = offer.TryGetProperty("flightNumber", out var fn)  ? fn.GetString()  ?? "" : "";
            var origin       = offer.TryGetProperty("origin",       out var or)  ? or.GetString()  ?? "" : "";
            var destination  = offer.TryGetProperty("destination",  out var dst) ? dst.GetString() ?? "" : "";
            var cabinCode    = offer.TryGetProperty("cabinCode",    out var cc)  ? cc.GetString()  ?? "" : "";
            var fareFamily   = offer.TryGetProperty("fareFamily",   out var ff)  ? ff.GetString()       : null;

            // Amounts — use the passenger-count-multiplied totals set when passengers were added
            var baseFareAmount = offer.TryGetProperty("baseFareAmount", out var bfa) ? bfa.GetDecimal() : 0m;
            var taxAmount      = offer.TryGetProperty("taxAmount",      out var ta)  ? ta.GetDecimal()  : 0m;
            var totalAmount    = offer.TryGetProperty("totalAmount",    out var tot) ? tot.GetDecimal() : 0m;

            // Points (reward bookings)
            var pointsPrice = 0;
            if (offer.TryGetProperty("pointsPrice", out var pp) && pp.ValueKind == JsonValueKind.Number)
                pointsPrice = pp.TryGetInt32(out var pts) ? pts : 0;

            // Build ISO 8601 datetime strings from separate date/time fields
            var departureDate = offer.TryGetProperty("departureDate", out var dd) ? dd.GetString() ?? "" : "";
            var departureTime = offer.TryGetProperty("departureTime", out var dt) ? dt.GetString() ?? "" : "";
            var arrivalTime   = offer.TryGetProperty("arrivalTime",   out var at) ? at.GetString() ?? "" : "";

            var departureDateTime = CombineDateTime(departureDate, departureTime);
            var arrivalDateTime   = CombineDateTime(departureDate, arrivalTime);

            flights.Add(new FlightOfferData
            {
                OfferId           = offerId,
                BasketItemId      = basketItemId,
                FlightNumber      = flightNumber,
                Origin            = origin,
                Destination       = destination,
                DepartureDateTime = departureDateTime,
                ArrivalDateTime   = arrivalDateTime,
                CabinCode         = cabinCode,
                FareFamily        = fareFamily,
                BaseFareAmount    = baseFareAmount,
                TaxAmount         = taxAmount,
                TotalAmount       = totalAmount,
                PointsPrice       = pointsPrice
            });
        }

        return flights;
    }

    private static List<PaymentSummaryPassenger> ParsePassengers(JsonElement root)
    {
        var passengers = new List<PaymentSummaryPassenger>();

        if (!root.TryGetProperty("passengers", out var paxEl) ||
            paxEl.ValueKind != JsonValueKind.Array)
            return passengers;

        foreach (var pax in paxEl.EnumerateArray())
        {
            passengers.Add(new PaymentSummaryPassenger
            {
                PassengerId = GetString(pax, "passengerId"),
                Type        = GetString(pax, "type"),
                GivenName   = GetString(pax, "givenName"),
                Surname     = GetString(pax, "surname")
            });
        }

        return passengers;
    }

    private static List<PaymentSummarySeatSelection> ParseSeatSelections(
        JsonElement root,
        IReadOnlyDictionary<string, string> flightNumberByItemId)
    {
        var seats = new List<PaymentSummarySeatSelection>();

        if (!root.TryGetProperty("seats", out var seatsEl) ||
            seatsEl.ValueKind != JsonValueKind.Array)
            return seats;

        foreach (var seat in seatsEl.EnumerateArray())
        {
            var basketItemRef = GetString(seat, "basketItemRef");
            var flightNumber  = flightNumberByItemId.TryGetValue(basketItemRef, out var fn) ? fn : "";

            var price = GetDecimal(seat, "price");
            var tax   = GetDecimal(seat, "tax");

            seats.Add(new PaymentSummarySeatSelection
            {
                PassengerId  = GetString(seat, "passengerId"),
                SeatNumber   = GetString(seat, "seatNumber"),
                SeatPosition = GetString(seat, "seatPosition"),
                FlightNumber = flightNumber,
                Price        = price,
                Tax          = tax,
                Currency     = GetString(seat, "currency")
            });
        }

        return seats;
    }

    private static List<PaymentSummaryBagSelection> ParseBagSelections(
        JsonElement root,
        IReadOnlyDictionary<string, string> flightNumberByItemId)
    {
        var bags = new List<PaymentSummaryBagSelection>();

        if (!root.TryGetProperty("bags", out var bagsEl) ||
            bagsEl.ValueKind != JsonValueKind.Array)
            return bags;

        foreach (var bag in bagsEl.EnumerateArray())
        {
            var basketItemRef = GetString(bag, "basketItemRef");
            var flightNumber  = flightNumberByItemId.TryGetValue(basketItemRef, out var fn) ? fn : "";

            var additionalBags = 0;
            if (bag.TryGetProperty("additionalBags", out var abEl) &&
                abEl.ValueKind == JsonValueKind.Number)
                abEl.TryGetInt32(out additionalBags);

            bags.Add(new PaymentSummaryBagSelection
            {
                PassengerId    = GetString(bag, "passengerId"),
                AdditionalBags = additionalBags,
                FlightNumber   = flightNumber,
                Price          = GetDecimal(bag, "price"),
                Tax            = GetDecimal(bag, "tax"),
                Currency       = GetString(bag, "currency")
            });
        }

        return bags;
    }

    private static List<PaymentSummaryProductSelection> ParseProductSelections(JsonElement root)
    {
        var products = new List<PaymentSummaryProductSelection>();

        if (!root.TryGetProperty("products", out var prodsEl) ||
            prodsEl.ValueKind != JsonValueKind.Array)
            return products;

        foreach (var prod in prodsEl.EnumerateArray())
        {
            string? segmentRef = null;
            if (prod.TryGetProperty("segmentRef", out var srEl) &&
                srEl.ValueKind == JsonValueKind.String)
                segmentRef = srEl.GetString();

            products.Add(new PaymentSummaryProductSelection
            {
                PassengerId = GetString(prod, "passengerId"),
                Name        = GetString(prod, "name"),
                Price       = GetDecimal(prod, "price"),
                Tax         = GetDecimal(prod, "tax"),
                Currency    = GetString(prod, "currency"),
                SegmentRef  = segmentRef
            });
        }

        return products;
    }

    private static List<PaymentSummarySsrSelection> ParseSsrSelections(JsonElement root)
    {
        var ssrs = new List<PaymentSummarySsrSelection>();

        if (!root.TryGetProperty("ssrSelections", out var ssrsEl) ||
            ssrsEl.ValueKind != JsonValueKind.Array)
            return ssrs;

        foreach (var ssr in ssrsEl.EnumerateArray())
        {
            // Angular sends `passengerRef`; fall back to `passengerId`
            var passengerId = ssr.TryGetProperty("passengerRef", out var prEl) && prEl.ValueKind == JsonValueKind.String
                ? prEl.GetString() ?? ""
                : GetString(ssr, "passengerId");

            ssrs.Add(new PaymentSummarySsrSelection
            {
                SsrCode     = GetString(ssr, "ssrCode"),
                PassengerId = passengerId
            });
        }

        return ssrs;
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static string CombineDateTime(string date, string time)
    {
        if (string.IsNullOrEmpty(date)) return string.Empty;
        var t = string.IsNullOrEmpty(time) ? "00:00" : time;
        // Ensure time has seconds
        if (t.Length == 5) t += ":00";
        return $"{date}T{t}";
    }

    private static string GetString(JsonElement el, string property) =>
        el.TryGetProperty(property, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() ?? ""
            : "";

    private static decimal GetDecimal(JsonElement el, string property)
    {
        if (!el.TryGetProperty(property, out var v)) return 0m;
        if (v.ValueKind == JsonValueKind.Number) return v.GetDecimal();
        if (v.ValueKind == JsonValueKind.String &&
            decimal.TryParse(v.GetString(),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out var d))
            return d;
        return 0m;
    }

    // ── Internal DTO ──────────────────────────────────────────────────────────

    private sealed class FlightOfferData
    {
        public Guid    OfferId           { get; init; }
        public string? BasketItemId      { get; init; }
        public string  FlightNumber      { get; init; } = string.Empty;
        public string  Origin            { get; init; } = string.Empty;
        public string  Destination       { get; init; } = string.Empty;
        public string  DepartureDateTime { get; init; } = string.Empty;
        public string  ArrivalDateTime   { get; init; } = string.Empty;
        public string  CabinCode         { get; init; } = string.Empty;
        public string? FareFamily        { get; init; }
        public decimal BaseFareAmount    { get; init; }
        public decimal TaxAmount         { get; init; }
        public decimal TotalAmount       { get; init; }
        public int     PointsPrice       { get; init; }
    }
}

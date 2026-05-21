using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using ReservationSystem.Shared.Common.Http;
using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;
using System.Net;
using System.Text.Json;

namespace ReservationSystem.Orchestration.Retail.Functions;

/// <summary>
/// HTTP-triggered function for retail product catalogue retrieval.
/// Fetches products and product groups from the Ancillary microservice in
/// parallel, then returns the catalogue pre-grouped as productGroups[] →
/// products[] so channels receive a ready-to-render structure.
///
/// When a basketId is supplied the availability rules stored on each product
/// are evaluated against the basket context (flights, cabin, passengers) and
/// only products whose rules are satisfied are returned. Products with no rules
/// are always included.
/// </summary>
public sealed class ProductsFunction
{
    private readonly ProductServiceClient _productServiceClient;
    private readonly OrderServiceClient _orderServiceClient;
    private readonly ILogger<ProductsFunction> _logger;

    public ProductsFunction(
        ProductServiceClient productServiceClient,
        OrderServiceClient orderServiceClient,
        ILogger<ProductsFunction> logger)
    {
        _productServiceClient = productServiceClient;
        _orderServiceClient   = orderServiceClient;
        _logger               = logger;
    }

    // -------------------------------------------------------------------------
    // GET /v1/products
    // -------------------------------------------------------------------------

    [Function("GetRetailProducts")]
    [OpenApiOperation(operationId: "GetRetailProducts", tags: new[] { "Products" }, Summary = "List all active retail products grouped by product group, with per-currency prices")]
    [OpenApiParameter(name: "channel",  In = Microsoft.OpenApi.Models.ParameterLocation.Query, Required = false, Type = typeof(string), Description = "Filter to products available on this channel (WEB, APP, NDC, GDS, KIOSK, CC, AIRPORT)")]
    [OpenApiParameter(name: "basketId", In = Microsoft.OpenApi.Models.ParameterLocation.Query, Required = false, Type = typeof(Guid),   Description = "When supplied, availability rules on each product are evaluated against this basket and non-matching products are excluded")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(object), Description = "Products with prices, pre-filtered by availability rules when basketId is provided")]
    public async Task<HttpResponseData> GetProducts(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/products")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var channelFilter = req.Query["channel"]?.Trim().ToUpperInvariant();

        // Resolve basket context for rule evaluation when basketId is provided
        BasketContext? basketContext = null;
        var basketIdRaw = req.Query["basketId"]?.Trim();
        if (!string.IsNullOrEmpty(basketIdRaw) && Guid.TryParse(basketIdRaw, out var basketId))
        {
            var basket = await _orderServiceClient.GetBasketAsync(basketId, cancellationToken);
            if (basket?.BasketData.HasValue == true)
                basketContext = ParseBasketContext(basket.BasketData.Value);
        }

        // Fetch products and product groups in parallel
        var productsTask = _productServiceClient.GetAllProductsAsync(cancellationToken);
        var groupsTask   = _productServiceClient.GetAllProductGroupsAsync(cancellationToken);
        await Task.WhenAll(productsTask, groupsTask);

        var productList = await productsTask;
        var groupList   = await groupsTask;

        if (productList is null || productList.Products.Count == 0)
            return await req.OkJsonAsync(new { productGroups = Array.Empty<object>() });

        var groupNames = groupList?.Groups
            .Where(g => g.IsActive)
            .ToDictionary(g => g.ProductGroupId, g => g.Name)
            ?? new Dictionary<Guid, string>();

        var grouped = productList.Products
            .Where(p => p.IsActive)
            .Where(p => channelFilter is null ||
                        (JsonSerializer.Deserialize<string[]>(p.AvailableChannels) ?? [])
                         .Any(c => c.Equals(channelFilter, StringComparison.OrdinalIgnoreCase)))
            .Where(p => basketContext is null || ProductPassesRules(p.AvailabilityRules, basketContext))
            .GroupBy(p => p.ProductGroupId)
            .Select(g => new
            {
                productGroupId   = g.Key,
                productGroupName = groupNames.TryGetValue(g.Key, out var gn) ? gn : string.Empty,
                products         = g.Select(p => new
                {
                    productId         = p.ProductId,
                    name              = p.Name,
                    description       = p.Description,
                    imageBase64       = p.ImageBase64,
                    ssrCode           = p.SsrCode,
                    isSegmentSpecific = p.IsSegmentSpecific,
                    prices            = p.Prices
                        .Where(pr => pr.IsActive)
                        .Select(pr => new
                        {
                            priceId      = pr.PriceId,
                            offerId      = pr.OfferId,
                            currencyCode = pr.CurrencyCode,
                            price        = pr.Price,
                            tax          = pr.Tax
                        })
                        .ToList()
                }).ToList()
            })
            .Where(g => g.products.Count > 0)
            .ToList();

        return await req.OkJsonAsync(new { productGroups = grouped });
    }

    // -------------------------------------------------------------------------
    // Basket context extraction
    // -------------------------------------------------------------------------

    private sealed record BasketContext(
        IReadOnlyList<FlightOfferContext> FlightOffers,
        IReadOnlyList<string> PassengerTypes);

    private sealed record FlightOfferContext(
        string Origin,
        string Destination,
        string CabinCode,
        string FlightNumber,
        string DepartureDate);

    private static BasketContext ParseBasketContext(JsonElement basketData)
    {
        var offers      = new List<FlightOfferContext>();
        var passengerTypes = new List<string>();

        if (basketData.TryGetProperty("flightOffers", out var offersEl) &&
            offersEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var o in offersEl.EnumerateArray())
            {
                offers.Add(new FlightOfferContext(
                    Origin:        GetString(o, "origin"),
                    Destination:   GetString(o, "destination"),
                    CabinCode:     GetString(o, "cabinCode"),
                    FlightNumber:  GetString(o, "flightNumber"),
                    DepartureDate: GetString(o, "departureDate")));
            }
        }

        if (basketData.TryGetProperty("passengers", out var passengersEl) &&
            passengersEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var p in passengersEl.EnumerateArray())
                passengerTypes.Add(GetString(p, "type").ToUpperInvariant());
        }

        return new BasketContext(offers, passengerTypes);
    }

    private static string GetString(JsonElement el, string property) =>
        el.TryGetProperty(property, out var v) ? v.GetString() ?? string.Empty : string.Empty;

    // -------------------------------------------------------------------------
    // Rule evaluation
    // -------------------------------------------------------------------------

    private static readonly string[] DayNames = ["SUN", "MON", "TUE", "WED", "THU", "FRI", "SAT"];

    private static bool ProductPassesRules(string? rulesJson, BasketContext ctx)
    {
        if (string.IsNullOrWhiteSpace(rulesJson)) return true;

        JsonElement[] rules;
        try
        {
            var parsed = JsonSerializer.Deserialize<JsonElement>(rulesJson);
            if (parsed.ValueKind != JsonValueKind.Array) return true;
            rules = [.. parsed.EnumerateArray()];
        }
        catch
        {
            return true;
        }

        if (rules.Length == 0) return true;

        // All rules must be satisfied (each rule is an AND of its conditions)
        return rules.All(rule => RulePasses(rule, ctx));
    }

    private static bool RulePasses(JsonElement rule, BasketContext ctx)
    {
        if (!rule.TryGetProperty("conditions", out var conditionsEl) ||
            conditionsEl.ValueKind != JsonValueKind.Array)
            return true;

        return conditionsEl.EnumerateArray().All(c => ConditionPasses(c, ctx));
    }

    private static bool ConditionPasses(JsonElement condition, BasketContext ctx)
    {
        var field    = condition.TryGetProperty("field",    out var f) ? f.GetString() ?? string.Empty : string.Empty;
        var op       = condition.TryGetProperty("operator", out var o) ? o.GetString() ?? string.Empty : string.Empty;
        var value    = condition.TryGetProperty("value",    out var v) ? v.GetString() ?? string.Empty : string.Empty;
        var upper    = value.ToUpperInvariant();
        var isNot    = op.Equals("isNot", StringComparison.OrdinalIgnoreCase);

        bool matches = field switch
        {
            "departureAirport" => ctx.FlightOffers.Any(o => o.Origin.ToUpperInvariant()        == upper),
            "arrivalAirport"   => ctx.FlightOffers.Any(o => o.Destination.ToUpperInvariant()   == upper),
            "cabinClass"       => ctx.FlightOffers.Any(o => o.CabinCode.ToUpperInvariant()     == upper),
            "flightNumber"     => ctx.FlightOffers.Any(o => o.FlightNumber.ToUpperInvariant()  == upper),
            "route"            => ctx.FlightOffers.Any(o => $"{o.Origin}-{o.Destination}".ToUpperInvariant() == upper),
            "passengerType"    => ctx.PassengerTypes.Any(t => t == upper),
            "dayOfWeek"        => ctx.FlightOffers.Count > 0 &&
                                   DateTime.TryParse(ctx.FlightOffers[0].DepartureDate, out var d) &&
                                   DayNames[(int)d.DayOfWeek] == upper,
            _                  => false
        };

        return isNot ? !matches : matches;
    }
}

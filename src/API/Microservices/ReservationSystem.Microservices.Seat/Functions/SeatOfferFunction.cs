using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using ReservationSystem.Microservices.Seat.Domain.Repositories;
using ReservationSystem.Microservices.Seat.Models.Responses;
using ReservationSystem.Shared.Common.Http;
using System.Net;
using System.Text.Json;
using System.Web;

namespace ReservationSystem.Microservices.Seat.Functions;

public sealed class SeatOfferFunction
{
    private readonly ISeatmapRepository _seatmapRepository;
    private readonly ISeatPricingRepository _seatPricingRepository;
    private readonly ILogger<SeatOfferFunction> _logger;

    public SeatOfferFunction(
        ISeatmapRepository seatmapRepository,
        ISeatPricingRepository seatPricingRepository,
        ILogger<SeatOfferFunction> logger)
    {
        _seatmapRepository = seatmapRepository;
        _seatPricingRepository = seatPricingRepository;
        _logger = logger;
    }

    /// <summary>
    /// GET /v1/seat-offers?flightId={flightId}
    /// Generate priced seat offers for all selectable seats on a flight.
    /// SeatOfferId format: so-{flightIdPrefix}-{seatNumber}-v1
    /// </summary>
    [Function("GetSeatOffers")]
    [OpenApiOperation(operationId: "GetSeatOffers", tags: new[] { "SeatOffers" }, Summary = "Get priced seat offers for a flight")]
    [OpenApiParameter(name: "flightId", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "The flight ID")]
    [OpenApiParameter(name: "aircraftType", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "The aircraft type code")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(SeatOffersResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> GetSeatOffers(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/seat-offers")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var query = HttpUtility.ParseQueryString(req.Url.Query);
        var flightIdStr = query["flightId"];

        if (string.IsNullOrWhiteSpace(flightIdStr) || !Guid.TryParse(flightIdStr, out var flightId))
            return await req.BadRequestAsync("Missing or malformed 'flightId' query parameter.");

        // For seat offer generation we need to know the aircraft type.
        // The flightId maps to an InventoryId. In the real system, we'd look up the aircraft type
        // from the Offer MS. Since the Seat MS cannot call other microservices, we need a way to
        // resolve this. The Retail API passes the aircraftType as a query param in practice, but
        // per the spec, we use flightId only. We'll check all active seatmaps and generate offers.
        // In a real implementation, the aircraftType would be resolved by the Retail API before calling.
        var aircraftTypeParam = query["aircraftType"];

        // If aircraftType is provided, use it directly. Otherwise fall back to first active seatmap.
        Domain.Entities.Seatmap? seatmap = null;
        if (!string.IsNullOrWhiteSpace(aircraftTypeParam))
        {
            seatmap = await _seatmapRepository.GetActiveByAircraftTypeCodeAsync(aircraftTypeParam, cancellationToken);
        }

        if (seatmap is null)
            return await req.NotFoundAsync("No active seatmap found for the aircraft type associated with this flight.");

        var activePricings = await _seatPricingRepository.GetAllActiveAsync(cancellationToken);
        var pricingLookup = activePricings.ToDictionary(p => (p.CabinCode, p.SeatPosition), p => p);

        var flightIdPrefix = flightId.ToString("N")[..8];

        // Parse CabinLayout JSON to extract selectable seats
        var seatOffers = new List<SeatOfferResponse>();
        using var doc = JsonDocument.Parse(seatmap.CabinLayout);

        foreach (var cabin in doc.RootElement.EnumerateArray())
        {
            var cabinCode = cabin.GetProperty("cabinCode").GetString() ?? "";
            if (!cabin.TryGetProperty("rows", out var rows)) continue;

            foreach (var row in rows.EnumerateArray())
            {
                if (!row.TryGetProperty("seats", out var seats)) continue;

                foreach (var seat in seats.EnumerateArray())
                {
                    var isSelectable = seat.TryGetProperty("isSelectable", out var sel) && sel.GetBoolean();
                    if (!isSelectable) continue;

                    var seatNumber = seat.GetProperty("seatNumber").GetString() ?? "";
                    var position = seat.GetProperty("position").GetString() ?? "";
                    var seatType = seat.TryGetProperty("type", out var typeProp) ? typeProp.GetString() ?? "Standard" : "Standard";
                    var attributes = new List<string>();
                    if (seat.TryGetProperty("attributes", out var attrProp))
                    {
                        foreach (var attr in attrProp.EnumerateArray())
                            attributes.Add(attr.GetString() ?? "");
                    }

                    // Determine pricing: F/J are free, W/Y use position-based pricing
                    var isChargeable = cabinCode is "W" or "Y";
                    decimal price = 0m;
                    var currencyCode = "GBP";

                    if (isChargeable && pricingLookup.TryGetValue((cabinCode, position), out var pricing))
                    {
                        price = pricing.Price;
                        currencyCode = pricing.CurrencyCode;
                    }

                    var seatOfferId = $"so-{flightIdPrefix}-{seatNumber}-v1";

                    seatOffers.Add(new SeatOfferResponse
                    {
                        SeatOfferId = seatOfferId,
                        SeatNumber = seatNumber,
                        CabinCode = cabinCode,
                        Position = position,
                        Type = seatType,
                        Attributes = attributes,
                        IsSelectable = true,
                        IsChargeable = isChargeable,
                        Price = price,
                        CurrencyCode = currencyCode
                    });
                }
            }
        }

        var response = new SeatOffersResponse
        {
            FlightId = flightId,
            AircraftType = seatmap.AircraftTypeCode,
            SeatOffers = seatOffers
        };

        return await req.OkJsonAsync(response);
    }

    /// <summary>
    /// GET /v1/seat-offers/{seatOfferId}
    /// Validate a specific seat offer by its deterministic ID.
    /// SeatOfferId format: so-{flightIdPrefix}-{seatNumber}-v1
    /// </summary>
    [Function("GetSeatOffer")]
    [OpenApiOperation(operationId: "GetSeatOffer", tags: new[] { "SeatOffers" }, Summary = "Validate a specific seat offer")]
    [OpenApiParameter(name: "seatOfferId", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "The seat offer ID")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(SeatOfferValidationResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> GetSeatOffer(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/seat-offers/{seatOfferId}")] HttpRequestData req,
        string seatOfferId,
        CancellationToken cancellationToken)
    {
        // Parse seatOfferId: so-{flightIdPrefix}-{seatNumber}-v1
        if (!seatOfferId.StartsWith("so-"))
            return await req.BadRequestAsync($"Malformed seatOfferId: '{seatOfferId}'.");

        // Remove "so-" prefix and "-v1" suffix
        var inner = seatOfferId[3..];
        if (!inner.EndsWith("-v1"))
            return await req.BadRequestAsync($"Malformed seatOfferId: '{seatOfferId}'.");
        inner = inner[..^3];

        // Split: flightIdPrefix-seatNumber (e.g., "3fa85f64-35A")
        var dashIndex = inner.IndexOf('-');
        if (dashIndex < 0)
            return await req.BadRequestAsync($"Malformed seatOfferId: cannot parse '{seatOfferId}'.");

        var flightIdPrefix = inner[..dashIndex];
        var seatNumber = inner[(dashIndex + 1)..];

        if (string.IsNullOrWhiteSpace(seatNumber))
            return await req.BadRequestAsync($"Malformed seatOfferId: no seat number in '{seatOfferId}'.");

        // Determine cabin code and position from seat number — we need to find it in a seatmap
        // For validation, we look through all active seatmaps to find the seat
        var allSeatmaps = await _seatmapRepository.GetAllAsync(cancellationToken);
        string? cabinCode = null;
        string? position = null;
        string? seatType = null;
        List<string>? attributes = null;

        foreach (var sm in allSeatmaps.Where(s => s.IsActive && !string.IsNullOrEmpty(s.CabinLayout)))
        {
            var fullSeatmap = await _seatmapRepository.GetByIdAsync(sm.SeatmapId, cancellationToken);
            if (fullSeatmap is null || string.IsNullOrEmpty(fullSeatmap.CabinLayout)) continue;

            try
            {
                using var doc = JsonDocument.Parse(fullSeatmap.CabinLayout);
                foreach (var cabin in doc.RootElement.EnumerateArray())
                {
                    if (!cabin.TryGetProperty("rows", out var rows)) continue;
                    foreach (var row in rows.EnumerateArray())
                    {
                        if (!row.TryGetProperty("seats", out var seats)) continue;
                        foreach (var seat in seats.EnumerateArray())
                        {
                            var sn = seat.GetProperty("seatNumber").GetString();
                            if (sn == seatNumber)
                            {
                                cabinCode = cabin.GetProperty("cabinCode").GetString();
                                position = seat.GetProperty("position").GetString();
                                seatType = seat.TryGetProperty("type", out var tp) ? tp.GetString() : "Standard";
                                attributes = new List<string>();
                                if (seat.TryGetProperty("attributes", out var attrProp))
                                    foreach (var attr in attrProp.EnumerateArray())
                                        attributes.Add(attr.GetString() ?? "");
                                break;
                            }
                        }
                        if (cabinCode is not null) break;
                    }
                    if (cabinCode is not null) break;
                }
            }
            catch { /* skip invalid layouts */ }
            if (cabinCode is not null) break;
        }

        // Determine pricing
        var isChargeable = cabinCode is "W" or "Y";
        decimal price = 0m;
        var currencyCode = "GBP";

        if (isChargeable && cabinCode is not null && position is not null)
        {
            var activePricings = await _seatPricingRepository.GetAllActiveAsync(cancellationToken);
            var pricing = activePricings.FirstOrDefault(p => p.CabinCode == cabinCode && p.SeatPosition == position);
            if (pricing is null)
                return await req.NotFoundAsync($"The pricing rule underlying offer '{seatOfferId}' is no longer active.");
            price = pricing.Price;
            currencyCode = pricing.CurrencyCode;
        }

        // Reconstruct flight ID from prefix
        var paddedId = flightIdPrefix.PadRight(32, '0');
        var flightId = Guid.Parse(paddedId[..8] + "-" + paddedId[8..12] + "-" + paddedId[12..16] + "-" + paddedId[16..20] + "-" + paddedId[20..32]);

        var response = new SeatOfferValidationResponse
        {
            SeatOfferId = seatOfferId,
            FlightId = flightId,
            SeatNumber = seatNumber,
            CabinCode = cabinCode ?? "",
            Position = position ?? "",
            Type = seatType ?? "Standard",
            Attributes = attributes ?? [],
            IsSelectable = true,
            IsChargeable = isChargeable,
            Price = price,
            CurrencyCode = currencyCode,
            IsValid = true
        };

        return await req.OkJsonAsync(response);
    }
}

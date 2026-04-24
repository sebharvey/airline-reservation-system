using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using ReservationSystem.Orchestration.Retail.Application.CheckInAncillaries;
using ReservationSystem.Orchestration.Retail.Application.ConfirmBasket;
using ReservationSystem.Shared.Common.Http;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ReservationSystem.Orchestration.Retail.Functions;

/// <summary>
/// HTTP-triggered functions for the online check-in ancillary payment flow.
/// </summary>
public sealed class CheckInFunction
{
    private readonly CheckInAncillariesHandler _ancillariesHandler;
    private readonly ILogger<CheckInFunction> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public CheckInFunction(CheckInAncillariesHandler ancillariesHandler, ILogger<CheckInFunction> logger)
    {
        _ancillariesHandler = ancillariesHandler;
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // POST /v1/checkin/{bookingRef}/ancillaries
    // -------------------------------------------------------------------------

    [Function("CheckInAncillaries")]
    [OpenApiOperation(operationId: "CheckInAncillaries", tags: new[] { "CheckIn" },
        Summary = "Purchase seat and/or bag ancillaries during online check-in; processes payment, updates the order, and issues EMDs")]
    [OpenApiParameter(name: "bookingRef", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "Booking reference")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(object), Required = true,
        Description = "{ basketId?, bagSelections, seatSelections, payment: { cardNumber, expiryDate, cvv, cardholderName } }")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(object), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> AddAncillaries(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/checkin/{bookingRef}/ancillaries")] HttpRequestData req,
        string bookingRef,
        CancellationToken ct)
    {
        JsonElement body;
        try { body = await JsonSerializer.DeserializeAsync<JsonElement>(req.Body, JsonOptions, ct); }
        catch (JsonException) { return await req.BadRequestAsync("Invalid JSON."); }

        if (string.IsNullOrWhiteSpace(bookingRef))
            return await req.BadRequestAsync("'bookingRef' is required.");

        // ── Parse optional basketId ───────────────────────────────────────────
        Guid? basketId = null;
        if (body.TryGetProperty("basketId", out var bidEl) && bidEl.TryGetGuid(out var bid))
            basketId = bid;

        // ── Parse payment ─────────────────────────────────────────────────────
        string? cardNumber = null, expiryDate = null, cvv = null, cardholderName = null, cardLast4 = null, cardType = null;
        if (body.TryGetProperty("payment", out var payEl) && payEl.ValueKind == JsonValueKind.Object)
        {
            cardNumber     = payEl.TryGetProperty("cardNumber",     out var cn)  ? cn.GetString()  : null;
            expiryDate     = payEl.TryGetProperty("expiryDate",     out var ed)  ? ed.GetString()  : null;
            cvv            = payEl.TryGetProperty("cvv",            out var cv)  ? cv.GetString()  : null;
            cardholderName = payEl.TryGetProperty("cardholderName", out var chn) ? chn.GetString() : null;
            cardLast4      = payEl.TryGetProperty("cardLast4",      out var cl4) ? cl4.GetString() : null;
            cardType       = payEl.TryGetProperty("cardType",       out var ctp) ? ctp.GetString() : null;
        }

        // ── Parse bag selections ──────────────────────────────────────────────
        var bags = new List<CheckInBagItem>();
        if (body.TryGetProperty("bagSelections", out var bagsEl) && bagsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var b in bagsEl.EnumerateArray())
            {
                var paxId  = b.TryGetProperty("passengerId",    out var p)  ? p.GetString()  ?? "" : "";
                var segId  = b.TryGetProperty("segmentId",      out var s)  ? s.GetString()  ?? "" : "";
                var offId  = b.TryGetProperty("bagOfferId",     out var bo) ? bo.GetString()       : null;
                var addB   = b.TryGetProperty("additionalBags", out var ab) ? ab.GetInt32()        : 1;
                var price  = b.TryGetProperty("price",          out var pr) ? pr.GetDecimal()      : 0m;
                var cur    = b.TryGetProperty("currency",       out var cu) ? cu.GetString()  ?? "GBP" : "GBP";
                if (!string.IsNullOrEmpty(paxId) && !string.IsNullOrEmpty(segId))
                    bags.Add(new CheckInBagItem(paxId, segId, offId, addB, price, cur));
            }
        }

        // ── Parse seat selections ─────────────────────────────────────────────
        var seats = new List<CheckInSeatItem>();
        if (body.TryGetProperty("seatSelections", out var seatsEl) && seatsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var s in seatsEl.EnumerateArray())
            {
                var paxId   = s.TryGetProperty("passengerId", out var p)  ? p.GetString()  ?? "" : "";
                var segId   = s.TryGetProperty("segmentId",   out var sg) ? sg.GetString() ?? "" : "";
                var seatNum = s.TryGetProperty("seatNumber",  out var sn) ? sn.GetString() ?? "" : "";
                var price   = s.TryGetProperty("seatPrice",   out var sp) ? sp.GetDecimal()     : 0m;
                var cur     = s.TryGetProperty("currency",    out var cu) ? cu.GetString()  ?? "GBP" : "GBP";
                if (!string.IsNullOrEmpty(paxId) && !string.IsNullOrEmpty(segId))
                    seats.Add(new CheckInSeatItem(paxId, segId, seatNum, price, cur));
            }
        }

        if (bags.Count == 0 && seats.Count == 0)
            return await req.BadRequestAsync("At least one bag or seat selection is required.");

        try
        {
            var command = new CheckInAncillariesCommand(
                bookingRef.ToUpperInvariant().Trim(),
                basketId, bags, seats,
                cardNumber, expiryDate, cvv, cardholderName, cardLast4, cardType);

            var result = await _ancillariesHandler.HandleAsync(command, ct);

            return await req.OkJsonAsync(new
            {
                success          = result.Success,
                paymentReference = result.PaymentReference,
                documents        = result.Documents.Select(d => new
                {
                    documentNumber = d.DocumentNumber,
                    documentType   = d.DocumentType,
                    passengerId    = d.PassengerId,
                    segmentRef     = d.SegmentRef,
                    amount         = d.Amount,
                    currency       = d.Currency
                })
            }, JsonOptions);
        }
        catch (PaymentValidationException ex)
        {
            return await req.BadRequestAsync(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Check-in ancillaries failed for {BookingRef}: {Message}", bookingRef, ex.Message);
            return await req.BadRequestAsync(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Check-in ancillaries unexpected error for {BookingRef}", bookingRef);
            return await req.InternalServerErrorAsync();
        }
    }
}

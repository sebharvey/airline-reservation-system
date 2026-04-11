using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Order.Application.GetOrder;
using ReservationSystem.Microservices.Order.Models.Mappers;
using ReservationSystem.Microservices.Order.Models.Requests;
using ReservationSystem.Microservices.Order.Models.Responses;
using ReservationSystem.Shared.Common.Http;
using ReservationSystem.Shared.Common.Json;
using System.Net;
using System.Text.Json;

namespace ReservationSystem.Microservices.Order.Functions;

/// <summary>
/// HTTP-triggered functions for the Online Check-In (OCI) journey.
/// </summary>
public sealed class OciFunction
{
    private readonly GetOrderHandler _getOrderHandler;
    private readonly ILogger<OciFunction> _logger;

    public OciFunction(GetOrderHandler getOrderHandler, ILogger<OciFunction> logger)
    {
        _getOrderHandler = getOrderHandler;
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // POST /v1/orders/oci/retrieve
    // -------------------------------------------------------------------------

    [Function("OciRetrieveOrder")]
    [OpenApiOperation(operationId: "OciRetrieveOrder", tags: new[] { "OCI" }, Summary = "Retrieve an order for online check-in by booking reference and surname")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(RetrieveOrderRequest), Required = true, Description = "The OCI retrieval request")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(OrderResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> OciRetrieveOrder(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/orders/oci/retrieve")] HttpRequestData req,
        CancellationToken ct)
    {
        JsonElement body;
        try { body = await JsonSerializer.DeserializeAsync<JsonElement>(req.Body, SharedJsonOptions.CamelCase, ct); }
        catch (JsonException) { return await req.BadRequestAsync("Invalid JSON."); }

        if (!body.TryGetProperty("bookingReference", out var bookingRefEl) || string.IsNullOrWhiteSpace(bookingRefEl.GetString()))
            return await req.BadRequestAsync("'bookingReference' is required.");

        if (!body.TryGetProperty("surname", out var surnameEl) || string.IsNullOrWhiteSpace(surnameEl.GetString()))
            return await req.BadRequestAsync("'surname' is required.");

        var bookingRef = bookingRefEl.GetString()!.ToUpperInvariant().Trim();
        var surname = surnameEl.GetString()!.Trim();

        var order = await _getOrderHandler.HandleAsync(new GetOrderQuery(bookingRef), ct);
        if (order is null)
            return await req.NotFoundAsync($"No order found for booking reference {bookingRef}.");

        // Validate surname against any passenger in the order
        try
        {
            using var doc = JsonDocument.Parse(order.OrderData);
            var hasMatch = false;
            if (doc.RootElement.TryGetProperty("dataLists", out var dataLists) &&
                dataLists.TryGetProperty("passengers", out var passengers))
            {
                foreach (var pax in passengers.EnumerateArray())
                {
                    if (pax.TryGetProperty("surname", out var sn) &&
                        string.Equals(sn.GetString(), surname, StringComparison.OrdinalIgnoreCase))
                    {
                        hasMatch = true;
                        break;
                    }
                }
            }
            if (!hasMatch)
                return await req.NotFoundAsync($"No order found for booking reference {bookingRef} and surname {surname}.");

            // Validate all passengers have e-ticket numbers before check-in can proceed
            var ticketedPaxIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (doc.RootElement.TryGetProperty("eTickets", out var eTickets))
            {
                foreach (var et in eTickets.EnumerateArray())
                {
                    if (et.TryGetProperty("passengerId", out var ePid) &&
                        et.TryGetProperty("eTicketNumber", out var eTn) &&
                        !string.IsNullOrWhiteSpace(eTn.GetString()))
                        ticketedPaxIds.Add(ePid.GetString()!);
                }
            }

            if (doc.RootElement.TryGetProperty("dataLists", out var dl) &&
                dl.TryGetProperty("passengers", out var paxList))
            {
                foreach (var pax in paxList.EnumerateArray())
                {
                    if (pax.TryGetProperty("passengerId", out var pPid) &&
                        !ticketedPaxIds.Contains(pPid.GetString() ?? ""))
                        return await req.UnprocessableEntityAsync(
                            $"Booking {bookingRef} has not been fully ticketed. Check-in is unavailable until all passengers have an e-ticket number.");
                }
            }
        }
        catch { }

        return await req.OkJsonAsync(OrderMapper.ToResponse(order));
    }
}

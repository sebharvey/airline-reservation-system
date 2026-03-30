using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using ReservationSystem.Shared.Common.Http;
using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices.Dto;
using System.Net;
using System.Web;

namespace ReservationSystem.Orchestration.Retail.Functions;

/// <summary>
/// HTTP-triggered functions for seatmap retrieval.
/// Orchestrates calls to the Seat microservice to return a merged seatmap with layout and pricing.
/// </summary>
public sealed class SeatmapFunction
{
    private readonly SeatServiceClient _seatServiceClient;
    private readonly ILogger<SeatmapFunction> _logger;

    public SeatmapFunction(SeatServiceClient seatServiceClient, ILogger<SeatmapFunction> logger)
    {
        _seatServiceClient = seatServiceClient;
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // GET /v1/flights/{flightId}/seatmap
    // -------------------------------------------------------------------------

    [Function("GetFlightSeatmap")]
    [OpenApiOperation(operationId: "GetFlightSeatmap", tags: new[] { "Seatmap" }, Summary = "Get seatmap with pricing for a flight")]
    [OpenApiParameter(name: "flightId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "The flight (inventory) identifier")]
    [OpenApiParameter(name: "aircraftType", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "The aircraft type code (e.g. A351)")]
    [OpenApiParameter(name: "flightNumber", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "The flight number for display purposes")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(object), Description = "Merged seatmap with layout and pricing")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> GetFlightSeatmap(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/flights/{flightId:guid}/seatmap")] HttpRequestData req,
        Guid flightId,
        CancellationToken cancellationToken)
    {
        var query = HttpUtility.ParseQueryString(req.Url.Query);
        var aircraftType = query["aircraftType"];
        var flightNumber = query["flightNumber"] ?? string.Empty;

        if (string.IsNullOrWhiteSpace(aircraftType))
            return await req.BadRequestAsync("'aircraftType' query parameter is required.");

        // Fetch layout and seat offers from Seat MS in parallel
        var layoutTask = _seatServiceClient.GetSeatmapAsync(aircraftType, cancellationToken);
        var offersTask = _seatServiceClient.GetSeatOffersAsync(flightId, aircraftType, cancellationToken);

        await Task.WhenAll(layoutTask, offersTask);

        var layout = await layoutTask;
        var offersResult = await offersTask;

        if (layout is null)
        {
            _logger.LogWarning("No active seatmap found for aircraft type '{AircraftType}'", aircraftType);
            return await req.NotFoundAsync($"No seatmap found for aircraft type '{aircraftType}'.");
        }

        // Build a lookup from seatNumber → offer for O(1) merge
        var offersByNumber = (offersResult?.SeatOffers ?? [])
            .ToDictionary(o => o.SeatNumber, o => o, StringComparer.OrdinalIgnoreCase);

        var cabins = BuildCabins(layout.Cabins, offersByNumber);

        var response = new
        {
            flightId,
            flightNumber,
            aircraftType = layout.AircraftType,
            cabins
        };

        return await req.OkJsonAsync(response);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static List<object> BuildCabins(
        IEnumerable<CabinLayoutDto> cabinLayouts,
        Dictionary<string, SeatOfferDto> offersByNumber)
    {
        var result = new List<object>();

        foreach (var cabin in cabinLayouts)
        {
            var seats = BuildSeats(cabin, offersByNumber);

            result.Add(new
            {
                cabinCode = cabin.CabinCode,
                cabinName = cabin.CabinName,
                columns = cabin.Columns,
                layout = cabin.Layout,
                startRow = cabin.StartRow,
                endRow = cabin.EndRow,
                seats
            });
        }

        return result;
    }

    private static List<object> BuildSeats(
        CabinLayoutDto cabin,
        Dictionary<string, SeatOfferDto> offersByNumber)
    {
        var seats = new List<object>();

        foreach (var row in cabin.Rows)
        {
            foreach (var seat in row.Seats)
            {
                if (offersByNumber.TryGetValue(seat.SeatNumber, out var offer))
                {
                    seats.Add(new
                    {
                        seatOfferId = offer.SeatOfferId,
                        seatNumber = seat.SeatNumber,
                        column = seat.Column,
                        rowNumber = row.RowNumber,
                        position = seat.Position,
                        cabinCode = cabin.CabinCode,
                        price = offer.Price,
                        currency = offer.CurrencyCode,
                        availability = "available",
                        attributes = seat.Attributes
                    });
                }
                else
                {
                    // Seat exists in layout but has no offer — treat as sold/unavailable
                    seats.Add(new
                    {
                        seatOfferId = string.Empty,
                        seatNumber = seat.SeatNumber,
                        column = seat.Column,
                        rowNumber = row.RowNumber,
                        position = seat.Position,
                        cabinCode = cabin.CabinCode,
                        price = 0m,
                        currency = "GBP",
                        availability = "sold",
                        attributes = seat.Attributes
                    });
                }
            }
        }

        return seats;
    }
}

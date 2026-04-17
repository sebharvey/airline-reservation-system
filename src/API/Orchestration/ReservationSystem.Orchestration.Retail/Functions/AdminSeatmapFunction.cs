using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using ReservationSystem.Shared.Common.Http;
using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices.Dto;
using System.Net;
using System.Text;
using System.Web;

namespace ReservationSystem.Orchestration.Retail.Functions;

/// <summary>
/// Staff-facing seatmap endpoint. Mirrors <see cref="SeatmapFunction"/> under the /admin
/// route prefix so that the TerminalAuthenticationMiddleware is applied automatically.
/// </summary>
public sealed class AdminSeatmapFunction
{
    private readonly SeatServiceClient _seatServiceClient;
    private readonly OfferServiceClient _offerServiceClient;
    private readonly ILogger<AdminSeatmapFunction> _logger;

    public AdminSeatmapFunction(
        SeatServiceClient seatServiceClient,
        OfferServiceClient offerServiceClient,
        ILogger<AdminSeatmapFunction> logger)
    {
        _seatServiceClient = seatServiceClient;
        _offerServiceClient = offerServiceClient;
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // GET /v1/admin/flights/{flightId}/seatmap
    // -------------------------------------------------------------------------

    [Function("AdminGetFlightSeatmap")]
    [OpenApiOperation(operationId: "AdminGetFlightSeatmap", tags: new[] { "Admin Booking" }, Summary = "Get seatmap with pricing for a flight (staff)")]
    [OpenApiParameter(name: "flightId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "The flight (inventory) identifier")]
    [OpenApiParameter(name: "aircraftType", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "The aircraft type code (e.g. A351)")]
    [OpenApiParameter(name: "flightNumber", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "The flight number for display purposes")]
    [OpenApiParameter(name: "cabinCode", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "IATA cabin code to filter results (e.g. Y, W, J, F)")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(object), Description = "Merged seatmap with layout and pricing")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Unauthorized, Description = "Unauthorized — staff JWT required")]
    public async Task<HttpResponseData> GetFlightSeatmap(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/flights/{flightId:guid}/seatmap")] HttpRequestData req,
        Guid flightId,
        CancellationToken cancellationToken)
    {
        var query = HttpUtility.ParseQueryString(req.Url.Query);
        var aircraftType = query["aircraftType"];
        var flightNumber = query["flightNumber"] ?? string.Empty;
        var cabinCode = query["cabinCode"];

        if (string.IsNullOrWhiteSpace(aircraftType))
            return await req.BadRequestAsync("'aircraftType' query parameter is required.");

        var layoutTask = _seatServiceClient.GetSeatmapAsync(aircraftType, cancellationToken);
        var offersTask = _seatServiceClient.GetSeatOffersAsync(flightId, aircraftType, cancellationToken);
        var holdsTask = _offerServiceClient.GetInventoryHoldsAsync(flightId, cancellationToken);
        var inventoryTask = _offerServiceClient.GetFlightByInventoryIdAsync(flightId, cancellationToken);

        await Task.WhenAll(layoutTask, offersTask, holdsTask, inventoryTask);

        var layout = await layoutTask;
        var offersResult = await offersTask;
        var holds = await holdsTask;
        var inventory = await inventoryTask;

        if (inventory is null)
        {
            _logger.LogWarning("Seatmap requested for unknown inventoryId '{FlightId}'", flightId);
            return await req.NotFoundAsync($"No flight inventory found for id '{flightId}'.");
        }

        if (layout is null)
        {
            _logger.LogWarning("No active seatmap found for aircraft type '{AircraftType}'", aircraftType);
            return await req.NotFoundAsync($"No seatmap found for aircraft type '{aircraftType}'.");
        }

        var offersByNumber = (offersResult?.SeatOffers ?? [])
            .ToDictionary(o => o.SeatNumber, o => o, StringComparer.OrdinalIgnoreCase);

        var heldSeatNumbers = holds
            .Where(h => !string.IsNullOrEmpty(h.SeatNumber))
            .Select(h => h.SeatNumber!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var cabins = BuildCabins(layout.Cabins, offersByNumber, heldSeatNumbers, inventory.FlightNumber, inventory.DepartureDate, cabinCode);

        var response = new
        {
            flightId,
            flightNumber,
            aircraftType = layout.AircraftType,
            cabins
        };

        return await req.OkJsonAsync(response);
    }

    private static List<object> BuildCabins(
        IEnumerable<CabinLayoutDto> cabinLayouts,
        Dictionary<string, SeatOfferDto> offersByNumber,
        HashSet<string> heldSeatNumbers,
        string flightNumber,
        string departureDate,
        string? cabinCodeFilter = null)
    {
        var result = new List<object>();

        var layouts = string.IsNullOrWhiteSpace(cabinCodeFilter)
            ? cabinLayouts
            : cabinLayouts.Where(c => string.Equals(c.CabinCode, cabinCodeFilter, StringComparison.OrdinalIgnoreCase));

        foreach (var cabin in layouts)
        {
            var seats = BuildSeats(cabin, offersByNumber, heldSeatNumbers, flightNumber, departureDate);
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
        Dictionary<string, SeatOfferDto> offersByNumber,
        HashSet<string> heldSeatNumbers,
        string flightNumber,
        string departureDate)
    {
        var seats = new List<object>();

        foreach (var row in cabin.Rows)
        {
            foreach (var seat in row.Seats)
            {
                if (heldSeatNumbers.Contains(seat.SeatNumber))
                {
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
                        availability = "held",
                        attributes = seat.Attributes
                    });
                }
                else if (offersByNumber.TryGetValue(seat.SeatNumber, out var offer))
                {
                    var seatOfferId = Convert.ToBase64String(Encoding.UTF8.GetBytes(
                        $"{flightNumber}-{departureDate}-{cabin.CabinCode}-{seat.SeatNumber}"));
                    seats.Add(new
                    {
                        seatOfferId,
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

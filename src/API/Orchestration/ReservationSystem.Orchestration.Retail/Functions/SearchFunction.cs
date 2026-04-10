using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using ReservationSystem.Shared.Common.Http;
using ReservationSystem.Orchestration.Retail.Application.SearchFlights;
using ReservationSystem.Orchestration.Retail.Models.Requests;
using ReservationSystem.Orchestration.Retail.Models.Responses;
using System.Net;

namespace ReservationSystem.Orchestration.Retail.Functions;

/// <summary>
/// HTTP-triggered functions for flight search.
/// Orchestrates calls to the Offer microservice to return available flights.
/// </summary>
public sealed class SearchFunction
{
    private readonly SearchFlightsHandler _searchHandler;
    private readonly SearchConnectingFlightsHandler _connectingHandler;
    private readonly ILogger<SearchFunction> _logger;

    public SearchFunction(
        SearchFlightsHandler searchHandler,
        SearchConnectingFlightsHandler connectingHandler,
        ILogger<SearchFunction> logger)
    {
        _searchHandler = searchHandler;
        _connectingHandler = connectingHandler;
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // POST /v1/search/slice
    // -------------------------------------------------------------------------

    [Function("SearchFlightsSlice")]
    [OpenApiOperation(operationId: "SearchFlightsSlice", tags: new[] { "Search" }, Summary = "Search for available flights for a single directional slice. Returns connecting itineraries via LHR automatically when no direct flight exists.")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(SearchSliceRequest), Required = true, Description = "Request body")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(SliceSearchResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    public async Task<HttpResponseData> SearchSlice(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/search/slice")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<SearchSliceRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        if (string.IsNullOrWhiteSpace(request!.Origin)
            || string.IsNullOrWhiteSpace(request.Destination)
            || string.IsNullOrWhiteSpace(request.DepartureDate)
            || request.PaxCount < 1)
        {
            return await req.BadRequestAsync("The fields 'origin', 'destination', 'departureDate', and 'paxCount' are required.");
        }

        var command = new SearchFlightsCommand(
            request.Origin,
            request.Destination,
            request.DepartureDate,
            request.PaxCount,
            request.BookingType);

        var result = await _searchHandler.HandleAsync(command, cancellationToken);
        return await req.OkJsonAsync(result);
    }

    // -------------------------------------------------------------------------
    // POST /v1/search/connecting
    // -------------------------------------------------------------------------

    [Function("SearchConnectingFlights")]
    [OpenApiOperation(operationId: "SearchConnectingFlights", tags: new[] { "Search" }, Summary = "Search for connecting itinerary options via the LHR hub")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(SearchConnectingRequest), Required = true, Description = "Request body")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(ConnectingSearchResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    public async Task<HttpResponseData> SearchConnecting(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/search/connecting")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<SearchConnectingRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        if (string.IsNullOrWhiteSpace(request!.Origin)
            || string.IsNullOrWhiteSpace(request.Destination)
            || string.IsNullOrWhiteSpace(request.DepartureDate)
            || request.PaxCount < 1)
        {
            return await req.BadRequestAsync("The fields 'origin', 'destination', 'departureDate', and 'paxCount' are required.");
        }

        var command = new SearchConnectingFlightsCommand(
            request.Origin,
            request.Destination,
            request.DepartureDate,
            request.PaxCount,
            request.BookingType);

        var result = await _connectingHandler.HandleAsync(command, cancellationToken);
        return await req.OkJsonAsync(result);
    }

    // -------------------------------------------------------------------------
    // POST /v1/search
    // -------------------------------------------------------------------------

    [Function("SearchFlights")]
    [OpenApiOperation(operationId: "SearchFlights", tags: new[] { "Search" }, Summary = "Search for available flights")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(SearchRequest), Required = true, Description = "Request body")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(SearchResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    public async Task<HttpResponseData> Search(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/search")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<SearchRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        if (string.IsNullOrWhiteSpace(request!.Origin)
            || string.IsNullOrWhiteSpace(request.Destination)
            || request.PassengerCount < 1)
        {
            return await req.BadRequestAsync("The fields 'origin', 'destination', and 'passengerCount' are required.");
        }

        var command = new SearchFlightsCommand(
            request.Origin,
            request.Destination,
            request.DepartureDate.ToString("yyyy-MM-dd"),
            request.PassengerCount,
            "Revenue");

        var result = await _searchHandler.HandleAsync(command, cancellationToken);
        return await req.OkJsonAsync(result);
    }
}

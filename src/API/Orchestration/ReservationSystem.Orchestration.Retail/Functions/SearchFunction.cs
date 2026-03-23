using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using ReservationSystem.Shared.Common.Http;
using ReservationSystem.Shared.Common.Json;
using ReservationSystem.Orchestration.Retail.Application.SearchFlights;
using ReservationSystem.Orchestration.Retail.Models.Requests;
using ReservationSystem.Orchestration.Retail.Models.Responses;
using System.Net;
using System.Text.Json;

namespace ReservationSystem.Orchestration.Retail.Functions;

/// <summary>
/// HTTP-triggered functions for flight search.
/// Orchestrates calls to the Offer microservice to return available flights.
/// </summary>
public sealed class SearchFunction
{
    private readonly SearchFlightsHandler _searchHandler;
    private readonly ILogger<SearchFunction> _logger;

    public SearchFunction(
        SearchFlightsHandler searchHandler,
        ILogger<SearchFunction> logger)
    {
        _searchHandler = searchHandler;
        _logger = logger;
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
        SearchRequest? request;

        try
        {
            request = await JsonSerializer.DeserializeAsync<SearchRequest>(
                req.Body, SharedJsonOptions.CamelCase, cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON in SearchFlights request");
            return await req.BadRequestAsync("Invalid JSON in request body.");
        }

        if (request is null
            || string.IsNullOrWhiteSpace(request.Origin)
            || string.IsNullOrWhiteSpace(request.Destination)
            || request.PassengerCount < 1)
        {
            return await req.BadRequestAsync("The fields 'origin', 'destination', and 'passengerCount' are required.");
        }

        var command = new SearchFlightsCommand(
            request.Origin,
            request.Destination,
            request.DepartureDate,
            request.ReturnDate,
            request.PassengerCount,
            request.CabinClass);

        var result = await _searchHandler.HandleAsync(command, cancellationToken);
        return await req.OkJsonAsync(result);
    }
}

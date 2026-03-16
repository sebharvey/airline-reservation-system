using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using ReservationSystem.Shared.Common.Http;
using ReservationSystem.Shared.Common.Json;
using ReservationSystem.Microservices.Offer.Application.CreateOffer;
using ReservationSystem.Microservices.Offer.Application.DeleteOffer;
using ReservationSystem.Microservices.Offer.Application.GetAllOffers;
using ReservationSystem.Microservices.Offer.Application.GetOffer;
using ReservationSystem.Microservices.Offer.Models.Mappers;
using ReservationSystem.Microservices.Offer.Models.Requests;
using System.Net;
using System.Text.Json;

namespace ReservationSystem.Microservices.Offer.Functions;

/// <summary>
/// HTTP-triggered functions for the Offer resource.
/// This is the "Presentation" layer in clean architecture — it translates
/// HTTP concerns (request parsing, status codes, serialisation) into
/// application-layer calls and back again.
/// </summary>
public sealed class OfferFunction
{
    private readonly GetOfferHandler _getHandler;
    private readonly GetAllOffersHandler _getAllHandler;
    private readonly CreateOfferHandler _createHandler;
    private readonly DeleteOfferHandler _deleteHandler;
    private readonly ILogger<OfferFunction> _logger;

    public OfferFunction(
        GetOfferHandler getHandler,
        GetAllOffersHandler getAllHandler,
        CreateOfferHandler createHandler,
        DeleteOfferHandler deleteHandler,
        ILogger<OfferFunction> logger)
    {
        _getHandler = getHandler;
        _getAllHandler = getAllHandler;
        _createHandler = createHandler;
        _deleteHandler = deleteHandler;
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // GET /v1/offers
    // -------------------------------------------------------------------------

    [Function("GetAllOffers")]
    public async Task<HttpResponseData> GetAll(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/offers")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var offers = await _getAllHandler.HandleAsync(new GetAllOffersQuery(), cancellationToken);
        var body = OfferMapper.ToResponse(offers);
        return await req.OkJsonAsync(body);
    }

    // -------------------------------------------------------------------------
    // GET /v1/offers/{id}
    // -------------------------------------------------------------------------

    [Function("GetOffer")]
    public async Task<HttpResponseData> GetById(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/offers/{id:guid}")] HttpRequestData req,
        Guid id,
        CancellationToken cancellationToken)
    {
        var offer = await _getHandler.HandleAsync(new GetOfferQuery(id), cancellationToken);

        if (offer is null)
            return req.CreateResponse(HttpStatusCode.NotFound);

        return await req.OkJsonAsync(OfferMapper.ToResponse(offer));
    }

    // -------------------------------------------------------------------------
    // POST /v1/offers
    // -------------------------------------------------------------------------

    [Function("CreateOffer")]
    public async Task<HttpResponseData> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/offers")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        CreateOfferRequest? request;

        try
        {
            request = await JsonSerializer.DeserializeAsync<CreateOfferRequest>(
                req.Body, SharedJsonOptions.CamelCase, cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON in CreateOffer request");
            return await req.BadRequestAsync("Invalid JSON in request body.");
        }

        if (request is null
            || string.IsNullOrWhiteSpace(request.FlightNumber)
            || string.IsNullOrWhiteSpace(request.Origin)
            || string.IsNullOrWhiteSpace(request.Destination)
            || string.IsNullOrWhiteSpace(request.FareClass)
            || string.IsNullOrWhiteSpace(request.Currency))
        {
            return await req.BadRequestAsync("The fields 'flightNumber', 'origin', 'destination', 'fareClass', and 'currency' are required.");
        }

        var command = OfferMapper.ToCommand(request);
        var created = await _createHandler.HandleAsync(command, cancellationToken);
        var response = OfferMapper.ToResponse(created);

        var httpResponse = req.CreateResponse(HttpStatusCode.Created);
        httpResponse.Headers.Add("Content-Type", "application/json");
        httpResponse.Headers.Add("Location", $"/v1/offers/{created.Id}");
        await httpResponse.WriteStringAsync(JsonSerializer.Serialize(response, SharedJsonOptions.CamelCase));
        return httpResponse;
    }

    // -------------------------------------------------------------------------
    // DELETE /v1/offers/{id}
    // -------------------------------------------------------------------------

    [Function("DeleteOffer")]
    public async Task<HttpResponseData> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/offers/{id:guid}")] HttpRequestData req,
        Guid id,
        CancellationToken cancellationToken)
    {
        var deleted = await _deleteHandler.HandleAsync(new DeleteOfferCommand(id), cancellationToken);

        return deleted
            ? req.CreateResponse(HttpStatusCode.NoContent)
            : req.CreateResponse(HttpStatusCode.NotFound);
    }
}

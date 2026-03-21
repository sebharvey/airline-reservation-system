using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Seat.Application.CreateSeatPricing;
using ReservationSystem.Microservices.Seat.Application.DeleteSeatPricing;
using ReservationSystem.Microservices.Seat.Application.GetAllSeatPricings;
using ReservationSystem.Microservices.Seat.Application.GetSeatPricing;
using ReservationSystem.Microservices.Seat.Application.UpdateSeatPricing;
using ReservationSystem.Microservices.Seat.Models.Mappers;
using ReservationSystem.Microservices.Seat.Models.Requests;

namespace ReservationSystem.Microservices.Seat.Functions;

/// <summary>
/// HTTP-triggered functions for SeatPricing admin resources.
/// Translates HTTP concerns into application-layer calls and back again.
/// </summary>
public sealed class SeatPricingFunction
{
    private readonly GetAllSeatPricingsHandler _getAllHandler;
    private readonly CreateSeatPricingHandler _createHandler;
    private readonly GetSeatPricingHandler _getHandler;
    private readonly UpdateSeatPricingHandler _updateHandler;
    private readonly DeleteSeatPricingHandler _deleteHandler;
    private readonly ILogger<SeatPricingFunction> _logger;

    public SeatPricingFunction(
        GetAllSeatPricingsHandler getAllHandler,
        CreateSeatPricingHandler createHandler,
        GetSeatPricingHandler getHandler,
        UpdateSeatPricingHandler updateHandler,
        DeleteSeatPricingHandler deleteHandler,
        ILogger<SeatPricingFunction> logger)
    {
        _getAllHandler = getAllHandler;
        _createHandler = createHandler;
        _getHandler = getHandler;
        _updateHandler = updateHandler;
        _deleteHandler = deleteHandler;
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // GET /v1/seat-pricing
    // -------------------------------------------------------------------------

    [Function("GetAllSeatPricings")]
    public async Task<HttpResponseData> GetAll(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/seat-pricing")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    // -------------------------------------------------------------------------
    // POST /v1/seat-pricing
    // -------------------------------------------------------------------------

    [Function("CreateSeatPricing")]
    public async Task<HttpResponseData> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/seat-pricing")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    // -------------------------------------------------------------------------
    // GET /v1/seat-pricing/{seatPricingId}
    // -------------------------------------------------------------------------

    [Function("GetSeatPricing")]
    public async Task<HttpResponseData> GetById(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/seat-pricing/{seatPricingId:guid}")] HttpRequestData req,
        Guid seatPricingId,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    // -------------------------------------------------------------------------
    // PUT /v1/seat-pricing/{seatPricingId}
    // -------------------------------------------------------------------------

    [Function("UpdateSeatPricing")]
    public async Task<HttpResponseData> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/seat-pricing/{seatPricingId:guid}")] HttpRequestData req,
        Guid seatPricingId,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    // -------------------------------------------------------------------------
    // DELETE /v1/seat-pricing/{seatPricingId}
    // -------------------------------------------------------------------------

    [Function("DeleteSeatPricing")]
    public async Task<HttpResponseData> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/seat-pricing/{seatPricingId:guid}")] HttpRequestData req,
        Guid seatPricingId,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}

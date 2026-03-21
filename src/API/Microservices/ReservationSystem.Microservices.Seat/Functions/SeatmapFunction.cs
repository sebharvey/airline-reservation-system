using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Seat.Application.CreateSeatmap;
using ReservationSystem.Microservices.Seat.Application.DeleteSeatmap;
using ReservationSystem.Microservices.Seat.Application.GetAllSeatmaps;
using ReservationSystem.Microservices.Seat.Application.GetSeatmap;
using ReservationSystem.Microservices.Seat.Application.GetSeatmapById;
using ReservationSystem.Microservices.Seat.Application.UpdateSeatmap;
using ReservationSystem.Microservices.Seat.Models.Mappers;
using ReservationSystem.Microservices.Seat.Models.Requests;

namespace ReservationSystem.Microservices.Seat.Functions;

/// <summary>
/// HTTP-triggered functions for Seatmap resources.
/// Translates HTTP concerns into application-layer calls and back again.
/// </summary>
public sealed class SeatmapFunction
{
    private readonly GetSeatmapHandler _getSeatmapHandler;
    private readonly GetAllSeatmapsHandler _getAllSeatmapsHandler;
    private readonly GetSeatmapByIdHandler _getSeatmapByIdHandler;
    private readonly CreateSeatmapHandler _createSeatmapHandler;
    private readonly UpdateSeatmapHandler _updateSeatmapHandler;
    private readonly DeleteSeatmapHandler _deleteSeatmapHandler;
    private readonly ILogger<SeatmapFunction> _logger;

    public SeatmapFunction(
        GetSeatmapHandler getSeatmapHandler,
        GetAllSeatmapsHandler getAllSeatmapsHandler,
        GetSeatmapByIdHandler getSeatmapByIdHandler,
        CreateSeatmapHandler createSeatmapHandler,
        UpdateSeatmapHandler updateSeatmapHandler,
        DeleteSeatmapHandler deleteSeatmapHandler,
        ILogger<SeatmapFunction> logger)
    {
        _getSeatmapHandler = getSeatmapHandler;
        _getAllSeatmapsHandler = getAllSeatmapsHandler;
        _getSeatmapByIdHandler = getSeatmapByIdHandler;
        _createSeatmapHandler = createSeatmapHandler;
        _updateSeatmapHandler = updateSeatmapHandler;
        _deleteSeatmapHandler = deleteSeatmapHandler;
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // GET /v1/seatmap/{aircraftType}
    // -------------------------------------------------------------------------

    [Function("GetSeatmap")]
    public async Task<HttpResponseData> GetSeatmap(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/seatmap/{aircraftType}")] HttpRequestData req,
        string aircraftType,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    // -------------------------------------------------------------------------
    // GET /v1/seatmaps
    // -------------------------------------------------------------------------

    [Function("GetAllSeatmaps")]
    public async Task<HttpResponseData> GetAllSeatmaps(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/seatmaps")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    // -------------------------------------------------------------------------
    // GET /v1/seatmaps/{seatmapId}
    // -------------------------------------------------------------------------

    [Function("GetSeatmapById")]
    public async Task<HttpResponseData> GetSeatmapById(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/seatmaps/{seatmapId:guid}")] HttpRequestData req,
        Guid seatmapId,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    // -------------------------------------------------------------------------
    // POST /v1/seatmaps
    // -------------------------------------------------------------------------

    [Function("CreateSeatmap")]
    public async Task<HttpResponseData> CreateSeatmap(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/seatmaps")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    // -------------------------------------------------------------------------
    // PUT /v1/seatmaps/{seatmapId}
    // -------------------------------------------------------------------------

    [Function("UpdateSeatmap")]
    public async Task<HttpResponseData> UpdateSeatmap(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/seatmaps/{seatmapId:guid}")] HttpRequestData req,
        Guid seatmapId,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    // -------------------------------------------------------------------------
    // DELETE /v1/seatmaps/{seatmapId}
    // -------------------------------------------------------------------------

    [Function("DeleteSeatmap")]
    public async Task<HttpResponseData> DeleteSeatmap(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/seatmaps/{seatmapId:guid}")] HttpRequestData req,
        Guid seatmapId,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Seat.Application.CreateAircraftType;
using ReservationSystem.Microservices.Seat.Application.GetAircraftType;
using ReservationSystem.Microservices.Seat.Application.GetAllAircraftTypes;
using ReservationSystem.Microservices.Seat.Application.DeleteAircraftType;
using ReservationSystem.Microservices.Seat.Application.UpdateAircraftType;
using ReservationSystem.Microservices.Seat.Models.Mappers;
using ReservationSystem.Microservices.Seat.Models.Requests;

namespace ReservationSystem.Microservices.Seat.Functions;

/// <summary>
/// HTTP-triggered functions for AircraftType admin resources.
/// Translates HTTP concerns into application-layer calls and back again.
/// </summary>
public sealed class AircraftTypeFunction
{
    private readonly GetAllAircraftTypesHandler _getAllHandler;
    private readonly CreateAircraftTypeHandler _createHandler;
    private readonly GetAircraftTypeHandler _getHandler;
    private readonly UpdateAircraftTypeHandler _updateHandler;
    private readonly DeleteAircraftTypeHandler _deleteHandler;
    private readonly ILogger<AircraftTypeFunction> _logger;

    public AircraftTypeFunction(
        GetAllAircraftTypesHandler getAllHandler,
        CreateAircraftTypeHandler createHandler,
        GetAircraftTypeHandler getHandler,
        UpdateAircraftTypeHandler updateHandler,
        DeleteAircraftTypeHandler deleteHandler,
        ILogger<AircraftTypeFunction> logger)
    {
        _getAllHandler = getAllHandler;
        _createHandler = createHandler;
        _getHandler = getHandler;
        _updateHandler = updateHandler;
        _deleteHandler = deleteHandler;
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // GET /v1/aircraft-types
    // -------------------------------------------------------------------------

    [Function("GetAllAircraftTypes")]
    public async Task<HttpResponseData> GetAll(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/aircraft-types")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    // -------------------------------------------------------------------------
    // POST /v1/aircraft-types
    // -------------------------------------------------------------------------

    [Function("CreateAircraftType")]
    public async Task<HttpResponseData> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/aircraft-types")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    // -------------------------------------------------------------------------
    // GET /v1/aircraft-types/{aircraftTypeCode}
    // -------------------------------------------------------------------------

    [Function("GetAircraftType")]
    public async Task<HttpResponseData> GetByCode(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/aircraft-types/{aircraftTypeCode}")] HttpRequestData req,
        string aircraftTypeCode,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    // -------------------------------------------------------------------------
    // PUT /v1/aircraft-types/{aircraftTypeCode}
    // -------------------------------------------------------------------------

    [Function("UpdateAircraftType")]
    public async Task<HttpResponseData> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/aircraft-types/{aircraftTypeCode}")] HttpRequestData req,
        string aircraftTypeCode,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    // -------------------------------------------------------------------------
    // DELETE /v1/aircraft-types/{aircraftTypeCode}
    // -------------------------------------------------------------------------

    [Function("DeleteAircraftType")]
    public async Task<HttpResponseData> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/aircraft-types/{aircraftTypeCode}")] HttpRequestData req,
        string aircraftTypeCode,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}

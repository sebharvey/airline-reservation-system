using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using ReservationSystem.Microservices.Seat.Application.CreateAircraftType;
using ReservationSystem.Microservices.Seat.Application.GetAircraftType;
using ReservationSystem.Microservices.Seat.Application.GetAllAircraftTypes;
using ReservationSystem.Microservices.Seat.Application.DeleteAircraftType;
using ReservationSystem.Microservices.Seat.Application.UpdateAircraftType;
using ReservationSystem.Microservices.Seat.Domain.Repositories;
using ReservationSystem.Microservices.Seat.Models.Mappers;
using ReservationSystem.Microservices.Seat.Models.Requests;
using ReservationSystem.Shared.Common.Http;
using ReservationSystem.Shared.Common.Json;
using System.Net;
using System.Text.Json;

namespace ReservationSystem.Microservices.Seat.Functions;

public sealed class AircraftTypeFunction
{
    private readonly GetAllAircraftTypesHandler _getAllHandler;
    private readonly CreateAircraftTypeHandler _createHandler;
    private readonly GetAircraftTypeHandler _getHandler;
    private readonly UpdateAircraftTypeHandler _updateHandler;
    private readonly DeleteAircraftTypeHandler _deleteHandler;
    private readonly ISeatmapRepository _seatmapRepository;
    private readonly IAircraftTypeRepository _aircraftTypeRepository;
    private readonly ILogger<AircraftTypeFunction> _logger;

    public AircraftTypeFunction(
        GetAllAircraftTypesHandler getAllHandler,
        CreateAircraftTypeHandler createHandler,
        GetAircraftTypeHandler getHandler,
        UpdateAircraftTypeHandler updateHandler,
        DeleteAircraftTypeHandler deleteHandler,
        ISeatmapRepository seatmapRepository,
        IAircraftTypeRepository aircraftTypeRepository,
        ILogger<AircraftTypeFunction> logger)
    {
        _getAllHandler = getAllHandler;
        _createHandler = createHandler;
        _getHandler = getHandler;
        _updateHandler = updateHandler;
        _deleteHandler = deleteHandler;
        _seatmapRepository = seatmapRepository;
        _aircraftTypeRepository = aircraftTypeRepository;
        _logger = logger;
    }

    [Function("GetAllAircraftTypes")]
    [OpenApiOperation(operationId: "GetAllAircraftTypes", tags: new[] { "AircraftTypes" }, Summary = "List all aircraft types")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(object), Description = "OK")]
    public async Task<HttpResponseData> GetAll(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/aircraft-types")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var types = await _getAllHandler.HandleAsync(new GetAllAircraftTypesQuery(), cancellationToken);
        return await req.OkJsonAsync(new { aircraftTypes = SeatMapper.ToResponse(types) });
    }

    [Function("CreateAircraftType")]
    [OpenApiOperation(operationId: "CreateAircraftType", tags: new[] { "AircraftTypes" }, Summary = "Create a new aircraft type")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(object), Required = true, Description = "The aircraft type to create")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.Created, contentType: "application/json", bodyType: typeof(object), Description = "Created")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Conflict, Description = "Conflict")]
    public async Task<HttpResponseData> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/aircraft-types")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        CreateAircraftTypeRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<CreateAircraftTypeRequest>(
                req.Body, SharedJsonOptions.CamelCase, cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON in CreateAircraftType request");
            return await req.BadRequestAsync("Invalid JSON in request body.");
        }

        if (request is null || string.IsNullOrWhiteSpace(request.AircraftTypeCode))
            return await req.BadRequestAsync("aircraftTypeCode is required.");

        if (request.AircraftTypeCode.Length != 4)
            return await req.BadRequestAsync("aircraftTypeCode must be exactly 4 characters.");

        if (string.IsNullOrWhiteSpace(request.Manufacturer))
            return await req.BadRequestAsync("manufacturer is required.");

        if (request.TotalSeats <= 0)
            return await req.BadRequestAsync("totalSeats must be > 0.");

        try
        {
            var command = SeatMapper.ToCommand(request);
            var created = await _createHandler.HandleAsync(command, cancellationToken);
            var response = SeatMapper.ToResponse(created);
            return await req.CreatedAsync($"/v1/aircraft-types/{created.AircraftTypeCode}", response);
        }
        catch (Exception ex) when (ex.Message.Contains("PRIMARY KEY") || ex.Message.Contains("duplicate") || ex.Message.Contains("UNIQUE"))
        {
            return await req.ConflictAsync($"An aircraft type with code '{request.AircraftTypeCode}' already exists.");
        }
    }

    [Function("GetAircraftType")]
    [OpenApiOperation(operationId: "GetAircraftType", tags: new[] { "AircraftTypes" }, Summary = "Get an aircraft type by code")]
    [OpenApiParameter(name: "aircraftTypeCode", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "The aircraft type code")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(object), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> GetByCode(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/aircraft-types/{aircraftTypeCode}")] HttpRequestData req,
        string aircraftTypeCode,
        CancellationToken cancellationToken)
    {
        var at = await _getHandler.HandleAsync(new GetAircraftTypeQuery(aircraftTypeCode), cancellationToken);
        if (at is null)
            return await req.NotFoundAsync($"No aircraft type found for code '{aircraftTypeCode}'.");
        return await req.OkJsonAsync(SeatMapper.ToResponse(at));
    }

    [Function("UpdateAircraftType")]
    [OpenApiOperation(operationId: "UpdateAircraftType", tags: new[] { "AircraftTypes" }, Summary = "Update an aircraft type")]
    [OpenApiParameter(name: "aircraftTypeCode", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "The aircraft type code")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(object), Required = true, Description = "The update request")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(object), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/aircraft-types/{aircraftTypeCode}")] HttpRequestData req,
        string aircraftTypeCode,
        CancellationToken cancellationToken)
    {
        UpdateAircraftTypeRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<UpdateAircraftTypeRequest>(
                req.Body, SharedJsonOptions.CamelCase, cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON in UpdateAircraftType request");
            return await req.BadRequestAsync("Invalid JSON in request body.");
        }

        if (request is null)
            return await req.BadRequestAsync("Request body is required.");

        var command = SeatMapper.ToCommand(aircraftTypeCode, request);
        var updated = await _updateHandler.HandleAsync(command, cancellationToken);

        if (updated is null)
            return await req.NotFoundAsync($"No aircraft type found for code '{aircraftTypeCode}'.");

        return await req.OkJsonAsync(SeatMapper.ToResponse(updated));
    }

    [Function("DeleteAircraftType")]
    [OpenApiOperation(operationId: "DeleteAircraftType", tags: new[] { "AircraftTypes" }, Summary = "Delete an aircraft type")]
    [OpenApiParameter(name: "aircraftTypeCode", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "The aircraft type code")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NoContent, Description = "Deleted")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Conflict, Description = "Conflict")]
    public async Task<HttpResponseData> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/aircraft-types/{aircraftTypeCode}")] HttpRequestData req,
        string aircraftTypeCode,
        CancellationToken cancellationToken)
    {
        // Guard: check for active seatmaps
        var hasActiveSeatmaps = await _seatmapRepository.HasActiveSeatmapsAsync(aircraftTypeCode, cancellationToken);
        if (hasActiveSeatmaps)
            return await req.ConflictAsync($"Cannot delete aircraft type '{aircraftTypeCode}' — active seatmaps reference it.");

        var deleted = await _aircraftTypeRepository.DeleteAsync(aircraftTypeCode, cancellationToken);
        return deleted
            ? req.NoContent()
            : await req.NotFoundAsync($"No aircraft type found for code '{aircraftTypeCode}'.");
    }
}

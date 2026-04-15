using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using ReservationSystem.Microservices.Ancillary.Application.Seat.CreateSeatmap;
using ReservationSystem.Microservices.Ancillary.Application.Seat.DeleteSeatmap;
using ReservationSystem.Microservices.Ancillary.Application.Seat.GetAllSeatmaps;
using ReservationSystem.Microservices.Ancillary.Application.Seat.GetSeatmap;
using ReservationSystem.Microservices.Ancillary.Application.Seat.GetSeatmapById;
using ReservationSystem.Microservices.Ancillary.Application.Seat.UpdateSeatmap;
using ReservationSystem.Microservices.Ancillary.Domain.Repositories.Seat;
using ReservationSystem.Microservices.Ancillary.Models.Seat.Mappers;
using ReservationSystem.Microservices.Ancillary.Models.Seat.Requests;
using ReservationSystem.Microservices.Ancillary.Models.Seat.Responses;
using ReservationSystem.Shared.Common.Caching;
using ReservationSystem.Shared.Common.Http;
using System.Net;
using System.Text.Json;

namespace ReservationSystem.Microservices.Ancillary.Functions.Seat;

public sealed class SeatmapFunction
{
    private readonly GetSeatmapHandler _getSeatmapHandler;
    private readonly GetAllSeatmapsHandler _getAllSeatmapsHandler;
    private readonly GetSeatmapByIdHandler _getSeatmapByIdHandler;
    private readonly CreateSeatmapHandler _createSeatmapHandler;
    private readonly UpdateSeatmapHandler _updateSeatmapHandler;
    private readonly DeleteSeatmapHandler _deleteSeatmapHandler;
    private readonly ISeatmapRepository _seatmapRepository;
    private readonly IAircraftTypeRepository _aircraftTypeRepository;
    private readonly ILogger<SeatmapFunction> _logger;

    public SeatmapFunction(
        GetSeatmapHandler getSeatmapHandler,
        GetAllSeatmapsHandler getAllSeatmapsHandler,
        GetSeatmapByIdHandler getSeatmapByIdHandler,
        CreateSeatmapHandler createSeatmapHandler,
        UpdateSeatmapHandler updateSeatmapHandler,
        DeleteSeatmapHandler deleteSeatmapHandler,
        ISeatmapRepository seatmapRepository,
        IAircraftTypeRepository aircraftTypeRepository,
        ILogger<SeatmapFunction> logger)
    {
        _getSeatmapHandler = getSeatmapHandler;
        _getAllSeatmapsHandler = getAllSeatmapsHandler;
        _getSeatmapByIdHandler = getSeatmapByIdHandler;
        _createSeatmapHandler = createSeatmapHandler;
        _updateSeatmapHandler = updateSeatmapHandler;
        _deleteSeatmapHandler = deleteSeatmapHandler;
        _seatmapRepository = seatmapRepository;
        _aircraftTypeRepository = aircraftTypeRepository;
        _logger = logger;
    }

    [Function("GetSeatmap")]
    [MicroserviceCache("Seatmap", 24)]
    [OpenApiOperation(operationId: "GetSeatmap", tags: new[] { "Seatmaps" }, Summary = "Get the active seatmap for an aircraft type")]
    [OpenApiParameter(name: "aircraftType", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "The aircraft type code")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(SeatmapResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> GetSeatmap(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/seatmap/{aircraftType}")] HttpRequestData req,
        string aircraftType,
        CancellationToken cancellationToken)
    {
        var seatmap = await _getSeatmapHandler.HandleAsync(new GetSeatmapQuery(aircraftType), cancellationToken);
        if (seatmap is null)
            return await req.NotFoundAsync($"No active seatmap found for aircraft type '{aircraftType}'.");

        var at = await _aircraftTypeRepository.GetByCodeAsync(aircraftType, cancellationToken);

        // Return the seatmap with parsed CabinLayout JSON
        var cabinLayoutJson = JsonSerializer.Deserialize<JsonElement>(seatmap.CabinLayout);
        var response = new
        {
            seatmapId = seatmap.SeatmapId,
            aircraftType = seatmap.AircraftTypeCode,
            version = seatmap.Version,
            totalSeats = at?.TotalSeats ?? 0,
            cabins = cabinLayoutJson
        };

        return await req.OkJsonAsync(response);
    }

    [Function("GetAllSeatmaps")]
    [MicroserviceCache("Seatmap", 24)]
    [OpenApiOperation(operationId: "GetAllSeatmaps", tags: new[] { "Seatmaps" }, Summary = "List all seatmaps")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(SeatmapListItemResponse[]), Description = "OK")]
    public async Task<HttpResponseData> GetAllSeatmaps(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/seatmaps")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var seatmaps = await _seatmapRepository.GetAllAsync(cancellationToken);
        var responses = seatmaps.Select(s => new
        {
            seatmapId = s.SeatmapId,
            aircraftTypeCode = s.AircraftTypeCode,
            version = s.Version,
            isActive = s.IsActive,
            createdAt = s.CreatedAt,
            updatedAt = s.UpdatedAt
        }).ToList();
        return await req.OkJsonAsync(new { seatmaps = responses });
    }

    [Function("GetSeatmapById")]
    [OpenApiOperation(operationId: "GetSeatmapById", tags: new[] { "Seatmaps" }, Summary = "Get a seatmap by ID")]
    [OpenApiParameter(name: "seatmapId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "The seatmap ID")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(SeatmapResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> GetSeatmapById(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/seatmaps/{seatmapId:guid}")] HttpRequestData req,
        Guid seatmapId,
        CancellationToken cancellationToken)
    {
        var seatmap = await _seatmapRepository.GetByIdAsync(seatmapId, cancellationToken);
        if (seatmap is null)
            return await req.NotFoundAsync($"No seatmap found for ID '{seatmapId}'.");
        return await req.OkJsonAsync(SeatMapper.ToResponse(seatmap));
    }

    [Function("CreateSeatmap")]
    [OpenApiOperation(operationId: "CreateSeatmap", tags: new[] { "Seatmaps" }, Summary = "Create a new seatmap")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(CreateSeatmapRequest), Required = true, Description = "The seatmap to create")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.Created, contentType: "application/json", bodyType: typeof(SeatmapResponse), Description = "Created")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    public async Task<HttpResponseData> CreateSeatmap(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/seatmaps")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<CreateSeatmapRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        if (string.IsNullOrWhiteSpace(request!.AircraftTypeCode))
            return await req.BadRequestAsync("aircraftTypeCode is required.");

        if (request.CabinLayout.ValueKind == JsonValueKind.Undefined)
            return await req.BadRequestAsync("cabinLayout is required.");

        var at = await _aircraftTypeRepository.GetByCodeAsync(request.AircraftTypeCode, cancellationToken);
        if (at is null)
            return await req.BadRequestAsync($"aircraftTypeCode '{request.AircraftTypeCode}' does not reference an existing aircraft type.");

        // Deactivate existing active seatmap for this aircraft type
        await _seatmapRepository.DeactivateByAircraftTypeAsync(request.AircraftTypeCode, cancellationToken);

        var command = SeatMapper.ToCommand(request);
        var created = await _createSeatmapHandler.HandleAsync(command, cancellationToken);
        var response = new
        {
            seatmapId = created.SeatmapId,
            aircraftTypeCode = created.AircraftTypeCode,
            version = created.Version,
            isActive = created.IsActive,
            createdAt = created.CreatedAt,
            updatedAt = created.UpdatedAt
        };
        return await req.CreatedAsync($"/v1/seatmaps/{created.SeatmapId}", response);
    }

    [Function("UpdateSeatmap")]
    [OpenApiOperation(operationId: "UpdateSeatmap", tags: new[] { "Seatmaps" }, Summary = "Update a seatmap")]
    [OpenApiParameter(name: "seatmapId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "The seatmap ID")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(UpdateSeatmapRequest), Required = true, Description = "The update request")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(SeatmapResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> UpdateSeatmap(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/seatmaps/{seatmapId:guid}")] HttpRequestData req,
        Guid seatmapId,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<UpdateSeatmapRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        var command = SeatMapper.ToCommand(seatmapId, request);
        var updated = await _updateSeatmapHandler.HandleAsync(command, cancellationToken);

        if (updated is null)
            return await req.NotFoundAsync($"No seatmap found for ID '{seatmapId}'.");

        return await req.OkJsonAsync(SeatMapper.ToResponse(updated));
    }

    [Function("DeleteSeatmap")]
    [OpenApiOperation(operationId: "DeleteSeatmap", tags: new[] { "Seatmaps" }, Summary = "Delete a seatmap")]
    [OpenApiParameter(name: "seatmapId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "The seatmap ID")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NoContent, Description = "Deleted")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> DeleteSeatmap(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/seatmaps/{seatmapId:guid}")] HttpRequestData req,
        Guid seatmapId,
        CancellationToken cancellationToken)
    {
        var deleted = await _seatmapRepository.DeleteAsync(seatmapId, cancellationToken);
        return deleted
            ? req.NoContent()
            : await req.NotFoundAsync($"No seatmap found for ID '{seatmapId}'.");
    }
}

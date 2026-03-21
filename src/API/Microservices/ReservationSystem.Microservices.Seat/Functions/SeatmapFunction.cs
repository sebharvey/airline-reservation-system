using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Seat.Application.CreateSeatmap;
using ReservationSystem.Microservices.Seat.Application.DeleteSeatmap;
using ReservationSystem.Microservices.Seat.Application.GetAllSeatmaps;
using ReservationSystem.Microservices.Seat.Application.GetSeatmap;
using ReservationSystem.Microservices.Seat.Application.GetSeatmapById;
using ReservationSystem.Microservices.Seat.Application.UpdateSeatmap;
using ReservationSystem.Microservices.Seat.Domain.Repositories;
using ReservationSystem.Microservices.Seat.Models.Mappers;
using ReservationSystem.Microservices.Seat.Models.Requests;
using ReservationSystem.Shared.Common.Http;
using ReservationSystem.Shared.Common.Json;
using System.Text.Json;

namespace ReservationSystem.Microservices.Seat.Functions;

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
    public async Task<HttpResponseData> CreateSeatmap(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/seatmaps")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        CreateSeatmapRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<CreateSeatmapRequest>(
                req.Body, SharedJsonOptions.CamelCase, cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON in CreateSeatmap request");
            return await req.BadRequestAsync("Invalid JSON in request body.");
        }

        if (request is null || string.IsNullOrWhiteSpace(request.AircraftTypeCode))
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
    public async Task<HttpResponseData> UpdateSeatmap(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/seatmaps/{seatmapId:guid}")] HttpRequestData req,
        Guid seatmapId,
        CancellationToken cancellationToken)
    {
        UpdateSeatmapRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<UpdateSeatmapRequest>(
                req.Body, SharedJsonOptions.CamelCase, cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON in UpdateSeatmap request");
            return await req.BadRequestAsync("Invalid JSON in request body.");
        }

        if (request is null)
            return await req.BadRequestAsync("Request body is required.");

        var command = SeatMapper.ToCommand(seatmapId, request);
        var updated = await _updateSeatmapHandler.HandleAsync(command, cancellationToken);

        if (updated is null)
            return await req.NotFoundAsync($"No seatmap found for ID '{seatmapId}'.");

        return await req.OkJsonAsync(SeatMapper.ToResponse(updated));
    }

    [Function("DeleteSeatmap")]
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

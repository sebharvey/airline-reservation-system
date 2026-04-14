using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Schedule.Application.GetSchedules;
using ReservationSystem.Microservices.Schedule.Application.ImportSchedules;
using ReservationSystem.Microservices.Schedule.Models.Mappers;
using ReservationSystem.Microservices.Schedule.Models.Requests;
using ReservationSystem.Microservices.Schedule.Models.Responses;
using ReservationSystem.Shared.Common.Caching;
using ReservationSystem.Shared.Common.Http;
using System.Net;

namespace ReservationSystem.Microservices.Schedule.Functions;

/// <summary>
/// HTTP-triggered function for bulk flight schedule import and retrieval.
/// Presentation layer — translates HTTP concerns into application-layer calls.
/// </summary>
public sealed class ScheduleFunction
{
    private readonly ImportSchedulesHandler _importHandler;
    private readonly GetSchedulesHandler _getSchedulesHandler;
    private readonly ILogger<ScheduleFunction> _logger;

    public ScheduleFunction(
        ImportSchedulesHandler importHandler,
        GetSchedulesHandler getSchedulesHandler,
        ILogger<ScheduleFunction> logger)
    {
        _importHandler = importHandler;
        _getSchedulesHandler = getSchedulesHandler;
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // POST /v1/schedules
    // -------------------------------------------------------------------------

    [Function("ImportSchedules")]
    [OpenApiOperation(operationId: "ImportSchedules", tags: new[] { "Schedules" }, Summary = "Bulk-import flight schedules into a schedule group from a season schedule payload")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(ImportSchedulesRequest), Required = true, Description = "Full season schedule payload containing scheduleGroupId, header, carriers, and schedule definitions")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(ImportSchedulesResponse), Description = "OK — returns count of imported and deleted records with per-schedule summary")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.InternalServerError, Description = "Internal Server Error")]
    public async Task<HttpResponseData> ImportSchedules(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/schedules")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<ImportSchedulesRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        // Validate scheduleGroupId.
        if (request.ScheduleGroupId == Guid.Empty)
            return await req.BadRequestAsync("'scheduleGroupId' is required.");

        // Validate carriers array is present and non-empty.
        if (request.Carriers is null || request.Carriers.Count == 0)
            return await req.BadRequestAsync("The 'carriers' array must contain at least one carrier.");

        // Flatten and validate individual schedules.
        var allSchedules = request.Carriers.SelectMany(c => c.Schedules).ToList();

        if (allSchedules.Count == 0)
            return await req.BadRequestAsync("At least one schedule definition must be provided across all carriers.");

        // Validate each schedule entry.
        for (var i = 0; i < allSchedules.Count; i++)
        {
            var s = allSchedules[i];

            if (string.IsNullOrWhiteSpace(s.FlightNumber))
                return await req.BadRequestAsync($"Schedule at index {i}: 'flightNumber' is required.");

            if (string.IsNullOrWhiteSpace(s.Origin) || s.Origin.Length != 3)
                return await req.BadRequestAsync($"Schedule '{s.FlightNumber}': 'origin' must be a 3-character IATA airport code.");

            if (string.IsNullOrWhiteSpace(s.Destination) || s.Destination.Length != 3)
                return await req.BadRequestAsync($"Schedule '{s.FlightNumber}': 'destination' must be a 3-character IATA airport code.");

            if (string.IsNullOrWhiteSpace(s.DepartureTime))
                return await req.BadRequestAsync($"Schedule '{s.FlightNumber}': 'departureTime' is required.");

            if (string.IsNullOrWhiteSpace(s.ArrivalTime))
                return await req.BadRequestAsync($"Schedule '{s.FlightNumber}': 'arrivalTime' is required.");

            if (string.IsNullOrWhiteSpace(s.AircraftType) || s.AircraftType.Length > 4)
                return await req.BadRequestAsync($"Schedule '{s.FlightNumber}': 'aircraftType' is required and must be at most 4 characters.");

            if (s.DaysOfWeek is < 1 or > 127)
                return await req.BadRequestAsync($"Schedule '{s.FlightNumber}': 'daysOfWeek' must be between 1 and 127.");

            if (string.IsNullOrWhiteSpace(s.ValidFrom) || string.IsNullOrWhiteSpace(s.ValidTo))
                return await req.BadRequestAsync($"Schedule '{s.FlightNumber}': 'validFrom' and 'validTo' are required.");

            if (!DateTime.TryParse(s.ValidFrom, out var validFrom) || !DateTime.TryParse(s.ValidTo, out var validTo))
                return await req.BadRequestAsync($"Schedule '{s.FlightNumber}': 'validFrom' and 'validTo' must be valid ISO 8601 dates (yyyy-MM-dd).");

            if (validFrom > validTo)
                return await req.BadRequestAsync($"Schedule '{s.FlightNumber}': 'validFrom' must not be after 'validTo'.");

            if (!TimeSpan.TryParse(s.DepartureTime, out _))
                return await req.BadRequestAsync($"Schedule '{s.FlightNumber}': 'departureTime' must be in HH:mm format.");

            if (!TimeSpan.TryParse(s.ArrivalTime, out _))
                return await req.BadRequestAsync($"Schedule '{s.FlightNumber}': 'arrivalTime' must be in HH:mm format.");

            if (string.IsNullOrWhiteSpace(s.CreatedBy))
                return await req.BadRequestAsync($"Schedule '{s.FlightNumber}': 'createdBy' is required.");
        }

        try
        {
            var command = ScheduleMapper.ToCommand(request);
            var (schedules, deleted) = await _importHandler.HandleAsync(command, cancellationToken);
            var response = ScheduleMapper.ToImportResponse(schedules, deleted);
            return await req.OkJsonAsync(response);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Validation error in ImportSchedules");
            return await req.BadRequestAsync(ex.Message);
        }
        catch (FormatException ex)
        {
            _logger.LogWarning(ex, "Invalid format in ImportSchedules request");
            return await req.BadRequestAsync("Invalid time or date format. Use HH:mm for times and yyyy-MM-dd for dates.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import schedules");
            return await req.InternalServerErrorAsync();
        }
    }

    // -------------------------------------------------------------------------
    // GET /v1/schedules
    // -------------------------------------------------------------------------

    [Function("GetSchedules")]
    [MicroserviceCache(1)]
    [OpenApiOperation(operationId: "GetSchedules", tags: new[] { "Schedules" }, Summary = "Retrieve persisted flight schedules, optionally filtered by schedule group")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(GetSchedulesResponse), Description = "OK — returns flight schedule records with operating date counts")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.InternalServerError, Description = "Internal Server Error")]
    public async Task<HttpResponseData> GetSchedules(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/schedules")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        try
        {
            var qs = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            Guid? scheduleGroupId = Guid.TryParse(qs["scheduleGroupId"], out var gid) ? gid : null;

            var schedules = await _getSchedulesHandler.HandleAsync(
                new GetSchedulesQuery(scheduleGroupId), cancellationToken);

            var response = new GetSchedulesResponse
            {
                Count = schedules.Count,
                Schedules = schedules.Select(s => new ScheduleItemResponse
                {
                    ScheduleId = s.ScheduleId,
                    ScheduleGroupId = s.ScheduleGroupId,
                    FlightNumber = s.FlightNumber,
                    Origin = s.Origin,
                    Destination = s.Destination,
                    DepartureTime = s.DepartureTime.ToString(@"hh\:mm"),
                    ArrivalTime = s.ArrivalTime.ToString(@"hh\:mm"),
                    ArrivalDayOffset = s.ArrivalDayOffset,
                    DepartureTimeUtc = s.DepartureTimeUtc?.ToString(@"hh\:mm"),
                    ArrivalTimeUtc = s.ArrivalTimeUtc?.ToString(@"hh\:mm"),
                    ArrivalDayOffsetUtc = s.ArrivalDayOffsetUtc,
                    DaysOfWeek = s.DaysOfWeek,
                    AircraftType = s.AircraftType,
                    ValidFrom = s.ValidFrom.ToString("yyyy-MM-dd"),
                    ValidTo = s.ValidTo.ToString("yyyy-MM-dd"),
                    FlightsCreated = s.FlightsCreated,
                    OperatingDateCount = s.GetOperatingDates().Count
                }).ToList().AsReadOnly()
            };

            return await req.OkJsonAsync(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve schedules");
            return await req.InternalServerErrorAsync();
        }
    }
}

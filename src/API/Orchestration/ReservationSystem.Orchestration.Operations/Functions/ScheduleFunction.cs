using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using ReservationSystem.Shared.Common.Http;
using ReservationSystem.Orchestration.Operations.Application.ImportSsim;
using ReservationSystem.Orchestration.Operations.Application.ImportSchedulesToInventory;
using ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Operations.Models.Requests;
using ReservationSystem.Orchestration.Operations.Models.Responses;
using System.Net;

namespace ReservationSystem.Orchestration.Operations.Functions;

/// <summary>
/// HTTP-triggered functions for flight schedule management.
/// Accepts SSIM file uploads, parses them within the Operations API,
/// and forwards the structured schedule payload to the Schedule microservice.
/// </summary>
public sealed class ScheduleFunction
{
    private readonly ImportSsimHandler _importSsimHandler;
    private readonly ImportSchedulesToInventoryHandler _importSchedulesToInventoryHandler;
    private readonly ScheduleServiceClient _scheduleServiceClient;
    private readonly ILogger<ScheduleFunction> _logger;

    public ScheduleFunction(
        ImportSsimHandler importSsimHandler,
        ImportSchedulesToInventoryHandler importSchedulesToInventoryHandler,
        ScheduleServiceClient scheduleServiceClient,
        ILogger<ScheduleFunction> logger)
    {
        _importSsimHandler = importSsimHandler;
        _importSchedulesToInventoryHandler = importSchedulesToInventoryHandler;
        _scheduleServiceClient = scheduleServiceClient;
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // GET /v1/schedules
    // -------------------------------------------------------------------------

    [Function("GetSchedules")]
    [OpenApiOperation(operationId: "GetSchedules", tags: new[] { "Schedules" }, Summary = "Retrieve all stored flight schedules, optionally filtered by schedule group")]
    [OpenApiParameter(name: "scheduleGroupId", In = ParameterLocation.Query, Required = false, Type = typeof(Guid), Description = "Filter schedules by schedule group")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(GetSchedulesResponse), Description = "OK — returns all flight schedules")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.InternalServerError, Description = "Internal Server Error")]
    public async Task<HttpResponseData> GetSchedules(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/schedules")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        try
        {
            var qs = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            Guid? scheduleGroupId = Guid.TryParse(qs["scheduleGroupId"], out var gid) ? gid : null;

            var result = await _scheduleServiceClient.GetSchedulesAsync(scheduleGroupId, cancellationToken);

            var response = new GetSchedulesResponse
            {
                Count = result.Count,
                Schedules = result.Schedules.Select(s => new ScheduleSummary
                {
                    ScheduleId = s.ScheduleId,
                    ScheduleGroupId = s.ScheduleGroupId,
                    FlightNumber = s.FlightNumber,
                    Origin = s.Origin,
                    Destination = s.Destination,
                    DepartureTime = s.DepartureTime,
                    ArrivalTime = s.ArrivalTime,
                    ArrivalDayOffset = s.ArrivalDayOffset,
                    DaysOfWeek = s.DaysOfWeek,
                    AircraftType = s.AircraftType,
                    ValidFrom = s.ValidFrom,
                    ValidTo = s.ValidTo,
                    FlightsCreated = s.FlightsCreated,
                    OperatingDateCount = s.OperatingDateCount
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

    // -------------------------------------------------------------------------
    // POST /v1/schedules/ssim
    // -------------------------------------------------------------------------

    [Function("ImportSsim")]
    [OpenApiOperation(operationId: "ImportSsim", tags: new[] { "Schedules" }, Summary = "Import schedules from an IATA SSIM Chapter 7 file into a schedule group")]
    [OpenApiParameter(name: "scheduleGroupId", In = ParameterLocation.Query, Required = true, Type = typeof(Guid), Description = "Target schedule group for the import")]
    [OpenApiParameter(name: "createdBy", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "Identity of the user performing the import (defaults to 'ssim-import')")]
    [OpenApiRequestBody(contentType: "text/plain", bodyType: typeof(string), Required = true, Description = "SSIM Chapter 7 plain-text file content")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(ImportSsimResponse), Description = "OK — returns count of imported and deleted records with per-schedule summary")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.InternalServerError, Description = "Internal Server Error")]
    public async Task<HttpResponseData> ImportSsim(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/schedules/ssim")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var qs = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var createdBy = qs["createdBy"] ?? "ssim-import";

        if (!Guid.TryParse(qs["scheduleGroupId"], out var scheduleGroupId))
            return await req.BadRequestAsync("'scheduleGroupId' query parameter is required.");

        string ssimText;
        using (var reader = new System.IO.StreamReader(req.Body))
        {
            ssimText = await reader.ReadToEndAsync(cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(ssimText))
            return await req.BadRequestAsync("Request body must contain SSIM file content.");

        try
        {
            var response = await _importSsimHandler.HandleAsync(
                new ImportSsimCommand(ssimText, createdBy, scheduleGroupId), cancellationToken);

            return await req.OkJsonAsync(response);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Validation error in ImportSsim");
            return await req.BadRequestAsync(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import SSIM file");
            return await req.InternalServerErrorAsync();
        }
    }

    // -------------------------------------------------------------------------
    // POST /v1/schedules/import-inventory
    // -------------------------------------------------------------------------

    [Function("ImportSchedulesToInventory")]
    [OpenApiOperation(operationId: "ImportSchedulesToInventory", tags: new[] { "Schedules" }, Summary = "Import schedules from the Schedule MS into offer inventory, optionally scoped to a schedule group")]
    [OpenApiParameter(name: "scheduleGroupId", In = ParameterLocation.Query, Required = false, Type = typeof(Guid), Description = "Limit inventory import to a specific schedule group")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(ImportSchedulesToInventoryRequest), Required = true, Description = "Cabin and fare definitions to apply when generating inventory for all stored schedules")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(ImportSchedulesToInventoryResponse), Description = "OK — returns counts of schedules processed, inventories created/skipped, and fares created")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request — missing or invalid cabin/fare definitions")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.InternalServerError, Description = "Internal Server Error")]
    public async Task<HttpResponseData> ImportSchedulesToInventory(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/schedules/import-inventory")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<ImportSchedulesToInventoryRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        var qs = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        Guid? scheduleGroupId = Guid.TryParse(qs["scheduleGroupId"], out var gid) ? gid : null;

        if (request.Cabins is null || request.Cabins.Count == 0)
            return await req.BadRequestAsync("'cabins' array must contain at least one cabin definition.");

        for (var i = 0; i < request.Cabins.Count; i++)
        {
            var cabin = request.Cabins[i];

            if (string.IsNullOrWhiteSpace(cabin.CabinCode) || cabin.CabinCode.Length != 1)
                return await req.BadRequestAsync($"Cabin at index {i}: 'cabinCode' must be a single character (F, J, W, or Y).");

            if (cabin.TotalSeats <= 0)
                return await req.BadRequestAsync($"Cabin '{cabin.CabinCode}': 'totalSeats' must be greater than zero.");

            if (cabin.Fares is null || cabin.Fares.Count == 0)
                return await req.BadRequestAsync($"Cabin '{cabin.CabinCode}': 'fares' must contain at least one fare definition.");

            for (var j = 0; j < cabin.Fares.Count; j++)
            {
                var fare = cabin.Fares[j];
                if (string.IsNullOrWhiteSpace(fare.FareBasisCode))
                    return await req.BadRequestAsync($"Cabin '{cabin.CabinCode}', fare at index {j}: 'fareBasisCode' is required.");
                if (string.IsNullOrWhiteSpace(fare.CurrencyCode))
                    return await req.BadRequestAsync($"Cabin '{cabin.CabinCode}', fare '{fare.FareBasisCode}': 'currencyCode' is required.");
            }
        }

        try
        {
            var command = new ImportSchedulesToInventoryCommand(
                request.Cabins.Select(c => new CabinDefinition(
                    c.CabinCode,
                    c.TotalSeats,
                    c.Fares.Select(f => new FareDefinition(
                        f.FareBasisCode,
                        f.FareFamily,
                        f.BookingClass,
                        f.CurrencyCode,
                        f.BaseFareAmount,
                        f.TaxAmount,
                        f.IsRefundable,
                        f.IsChangeable,
                        f.ChangeFeeAmount,
                        f.CancellationFeeAmount,
                        f.PointsPrice,
                        f.PointsTaxes)).ToList().AsReadOnly()
                )).ToList().AsReadOnly(),
                scheduleGroupId);

            var response = await _importSchedulesToInventoryHandler.HandleAsync(command, cancellationToken);

            return await req.OkJsonAsync(response);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Validation error in ImportSchedulesToInventory");
            return await req.BadRequestAsync(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import schedules to inventory");
            return await req.InternalServerErrorAsync();
        }
    }
}

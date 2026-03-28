using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using ReservationSystem.Shared.Common.Http;
using ReservationSystem.Orchestration.Operations.Application.ImportSsim;
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
    private readonly ILogger<ScheduleFunction> _logger;

    public ScheduleFunction(
        ImportSsimHandler importSsimHandler,
        ILogger<ScheduleFunction> logger)
    {
        _importSsimHandler = importSsimHandler;
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // POST /v1/schedules/ssim
    // -------------------------------------------------------------------------

    [Function("ImportSsim")]
    [OpenApiOperation(operationId: "ImportSsim", tags: new[] { "Schedules" }, Summary = "Import schedules from an IATA SSIM Chapter 7 file")]
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
                new ImportSsimCommand(ssimText, createdBy), cancellationToken);

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
}

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Exceptions.Application.GetExceptions;
using ReservationSystem.Microservices.Exceptions.Models.Responses;
using ReservationSystem.Shared.Common.Http;
using System.Net;

namespace ReservationSystem.Microservices.Exceptions.Functions;

public sealed class ExceptionsFunction
{
    private readonly GetExceptionsHandler _getExceptionsHandler;
    private readonly ILogger<ExceptionsFunction> _logger;

    public ExceptionsFunction(
        GetExceptionsHandler getExceptionsHandler,
        ILogger<ExceptionsFunction> logger)
    {
        _getExceptionsHandler = getExceptionsHandler;
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // GET /v1/exceptions
    // -------------------------------------------------------------------------

    [Function("GetExceptions")]
    [OpenApiOperation(operationId: "GetExceptions", tags: new[] { "Exceptions" }, Summary = "Retrieve all Application Insights exceptions from the last hour with call stacks")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(GetExceptionsResponse), Description = "OK — returns exceptions from the last hour")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.InternalServerError, Description = "Internal Server Error")]
    public async Task<HttpResponseData> GetExceptions(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/exceptions")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _getExceptionsHandler.HandleAsync(cancellationToken);
            return await req.OkJsonAsync(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve exceptions from Application Insights");
            return await req.InternalServerErrorAsync();
        }
    }
}

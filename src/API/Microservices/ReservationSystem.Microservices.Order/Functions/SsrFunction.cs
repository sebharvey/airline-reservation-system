using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using ReservationSystem.Microservices.Order.Application.GetSsrOptions;
using ReservationSystem.Microservices.Order.Models.Responses;
using ReservationSystem.Shared.Common.Http;
using System.Net;

namespace ReservationSystem.Microservices.Order.Functions;

public sealed class SsrFunction
{
    private readonly GetSsrOptionsHandler _getSsrOptionsHandler;
    private readonly ILogger<SsrFunction> _logger;

    public SsrFunction(GetSsrOptionsHandler getSsrOptionsHandler, ILogger<SsrFunction> logger)
    {
        _getSsrOptionsHandler = getSsrOptionsHandler;
        _logger = logger;
    }

    // GET /v1/ssr/options
    [Function("GetSsrOptions")]
    [OpenApiOperation(operationId: "GetSsrOptions", tags: new[] { "SSR" }, Summary = "Retrieve all active SSR codes and labels by category")]
    [OpenApiParameter(name: "cabinCode", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "Filter SSRs applicable to a specific cabin")]
    [OpenApiParameter(name: "flightNumbers", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "Comma-separated flight numbers to filter applicable SSRs")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(GetSsrOptionsResponse), Description = "OK")]
    public async Task<HttpResponseData> GetSsrOptions(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/ssr/options")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var qs = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var query = new GetSsrOptionsQuery(qs["cabinCode"], qs["flightNumbers"]);

        var entries = await _getSsrOptionsHandler.HandleAsync(query, cancellationToken);

        var response = new GetSsrOptionsResponse(
            entries.Select(e => new SsrOptionDto(e.SsrCode, e.Label, e.Category))
                   .ToList()
                   .AsReadOnly());

        return await req.OkJsonAsync(response);
    }
}

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using System.Net;
using System.Text.Json;

namespace ReservationSystem.Template.TemplateApi.Functions;

public class HelloWorldFunction
{
    private readonly ILogger<HelloWorldFunction> _logger;

    public HelloWorldFunction(ILogger<HelloWorldFunction> logger)
    {
        _logger = logger;
    }

    [Function("HelloWorld")]
    [OpenApiOperation(operationId: "HelloWorld", tags: new[] { "Health" }, Summary = "Smoke-test endpoint", Description = "Returns a Hello World message to verify the function host is running.")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(object), Description = "OK")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/hello")] HttpRequestData req)
    {
        _logger.LogInformation("HelloWorld function triggered.");

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json");

        var result = new { Message = "Hello, World!" };
        await response.WriteStringAsync(JsonSerializer.Serialize(result));

        return response;
    }
}

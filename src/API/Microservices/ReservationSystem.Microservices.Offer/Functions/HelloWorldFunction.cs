using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace ReservationSystem.Microservices.Offer.Functions;

public class HelloWorldFunction
{
    private readonly ILogger<HelloWorldFunction> _logger;

    public HelloWorldFunction(ILogger<HelloWorldFunction> logger)
    {
        _logger = logger;
    }

    [Function("HelloWorld")]
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

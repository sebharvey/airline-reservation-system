using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using ReservationSystem.Template.TemplateApi.Swagger;
using System.Net;

namespace ReservationSystem.Template.TemplateApi.Functions;

public sealed class SwaggerFunction
{
    [Function("SwaggerJson")]
    public async Task<HttpResponseData> GetSwaggerJson(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "swagger.json")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await response.WriteStringAsync(SwaggerDocument.Json, cancellationToken);
        return response;
    }
}

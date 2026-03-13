using Microsoft.Azure.Functions.Worker.Http;
using ReservationSystem.Shared.Common.Json;
using System.Net;
using System.Text.Json;

namespace ReservationSystem.Shared.Common.Http;

/// <summary>
/// Extension methods for <see cref="HttpRequestData"/> to simplify building
/// consistent HTTP responses across all Azure Functions APIs.
/// </summary>
public static class HttpResponseExtensions
{
    /// <summary>
    /// Creates a 200 OK response with a JSON-serialised body and camelCase naming.
    /// </summary>
    public static async Task<HttpResponseData> OkJsonAsync<T>(this HttpRequestData req, T body)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json");
        await response.WriteStringAsync(JsonSerializer.Serialize(body, SharedJsonOptions.CamelCase));
        return response;
    }

    /// <summary>
    /// Creates a 400 Bad Request response with a JSON error body.
    /// </summary>
    public static async Task<HttpResponseData> BadRequestAsync(this HttpRequestData req, string message)
    {
        var response = req.CreateResponse(HttpStatusCode.BadRequest);
        response.Headers.Add("Content-Type", "application/json");
        await response.WriteStringAsync(
            JsonSerializer.Serialize(new { error = message }, SharedJsonOptions.CamelCase));
        return response;
    }
}

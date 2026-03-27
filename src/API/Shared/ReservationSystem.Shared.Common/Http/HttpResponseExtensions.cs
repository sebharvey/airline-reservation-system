using Microsoft.Azure.Functions.Worker.Http;
using ReservationSystem.Shared.Common.Json;
using System.Net;
using System.Text.Json;

namespace ReservationSystem.Shared.Common.Http;

/// <summary>
/// Extension methods for <see cref="HttpRequestData"/> to simplify building
/// consistent HTTP responses across all Azure Functions APIs.
///
/// Usage pattern — inject nothing, call directly on the request object:
/// <code>
///   return await req.OkJsonAsync(response);
///   return await req.CreatedAsync($"/v1/orders/{id}", response);
///   return await req.NotFoundAsync("Order not found");
/// </code>
///
/// All JSON bodies use <see cref="SharedJsonOptions.CamelCase"/> so property
/// names automatically follow the API convention without manual annotation.
/// </summary>
public static class HttpResponseExtensions
{
    // -------------------------------------------------------------------------
    // 2xx Success
    // -------------------------------------------------------------------------

    /// <summary>
    /// 200 OK — successful read or update. Serialises <paramref name="body"/>
    /// as camelCase JSON.
    /// </summary>
    public static async Task<HttpResponseData> OkJsonAsync<T>(this HttpRequestData req, T body)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json");
        await response.WriteStringAsync(JsonSerializer.Serialize(body, SharedJsonOptions.CamelCase));
        return response;
    }

    /// <summary>
    /// 201 Created — resource successfully created.
    /// Sets the <c>Location</c> header to <paramref name="resourceUri"/> so
    /// clients can navigate directly to the new resource.
    /// Serialises <paramref name="body"/> as camelCase JSON.
    /// </summary>
    /// <param name="resourceUri">
    /// Relative path of the created resource, e.g. <c>/v1/orders/abc-123</c>.
    /// </param>
    public static async Task<HttpResponseData> CreatedAsync<T>(
        this HttpRequestData req, string resourceUri, T body)
    {
        var response = req.CreateResponse(HttpStatusCode.Created);
        response.Headers.Add("Content-Type", "application/json");
        response.Headers.Add("Location", resourceUri);
        await response.WriteStringAsync(JsonSerializer.Serialize(body, SharedJsonOptions.CamelCase));
        return response;
    }

    /// <summary>
    /// 204 No Content — successful operation with no response body.
    /// Use for DELETE and PATCH operations that do not return the updated resource.
    /// </summary>
    public static HttpResponseData NoContent(this HttpRequestData req)
        => req.CreateResponse(HttpStatusCode.NoContent);

    // -------------------------------------------------------------------------
    // 4xx Client Errors
    // -------------------------------------------------------------------------

    /// <summary>
    /// 400 Bad Request — malformed request or failed input validation.
    /// Returns an <see cref="ApiError"/> JSON body with the supplied message.
    /// </summary>
    public static async Task<HttpResponseData> BadRequestAsync(
        this HttpRequestData req, string message, string? correlationId = null)
    {
        var response = req.CreateResponse(HttpStatusCode.BadRequest);
        response.Headers.Add("Content-Type", "application/json");
        await response.WriteStringAsync(
            JsonSerializer.Serialize(
                ApiError.From(message, errorCode: "BAD_REQUEST", correlationId: correlationId),
                SharedJsonOptions.CamelCase));
        return response;
    }

    /// <summary>
    /// 404 Not Found — the requested resource does not exist.
    /// </summary>
    public static async Task<HttpResponseData> NotFoundAsync(
        this HttpRequestData req, string message, string? correlationId = null)
    {
        var response = req.CreateResponse(HttpStatusCode.NotFound);
        response.Headers.Add("Content-Type", "application/json");
        await response.WriteStringAsync(
            JsonSerializer.Serialize(
                ApiError.From(message, errorCode: "NOT_FOUND", correlationId: correlationId),
                SharedJsonOptions.CamelCase));
        return response;
    }

    /// <summary>
    /// 409 Conflict — the request conflicts with current resource state.
    /// Use when a duplicate is detected (e.g. duplicate booking reference,
    /// replayed non-idempotent request) or a state-machine transition is invalid
    /// (e.g. attempting to cancel an already-cancelled order).
    /// </summary>
    public static async Task<HttpResponseData> ConflictAsync(
        this HttpRequestData req, string message, string? correlationId = null)
    {
        var response = req.CreateResponse(HttpStatusCode.Conflict);
        response.Headers.Add("Content-Type", "application/json");
        await response.WriteStringAsync(
            JsonSerializer.Serialize(
                ApiError.From(message, errorCode: "CONFLICT", correlationId: correlationId),
                SharedJsonOptions.CamelCase));
        return response;
    }

    /// <summary>
    /// 410 Gone — the requested resource existed but is no longer available.
    /// Use when an offer has expired or been consumed and cannot be used.
    /// </summary>
    public static async Task<HttpResponseData> GoneAsync(
        this HttpRequestData req, string message, string? correlationId = null)
    {
        var response = req.CreateResponse(HttpStatusCode.Gone);
        response.Headers.Add("Content-Type", "application/json");
        await response.WriteStringAsync(
            JsonSerializer.Serialize(
                ApiError.From(message, errorCode: "GONE", correlationId: correlationId),
                SharedJsonOptions.CamelCase));
        return response;
    }

    /// <summary>
    /// 422 Unprocessable Entity — the request is syntactically valid but
    /// semantically incorrect (e.g. departure date in the past, selected seat
    /// not available on that aircraft type, loyalty points balance insufficient).
    /// Prefer this over 400 when the payload is structurally correct but
    /// violates business rules.
    /// </summary>
    public static async Task<HttpResponseData> UnprocessableEntityAsync(
        this HttpRequestData req, string message, string? correlationId = null)
    {
        var response = req.CreateResponse(HttpStatusCode.UnprocessableEntity);
        response.Headers.Add("Content-Type", "application/json");
        await response.WriteStringAsync(
            JsonSerializer.Serialize(
                ApiError.From(message, errorCode: "UNPROCESSABLE", correlationId: correlationId),
                SharedJsonOptions.CamelCase));
        return response;
    }

    /// <summary>
    /// 401 Unauthorized — authentication required or credentials invalid.
    /// </summary>
    public static async Task<HttpResponseData> UnauthorizedAsync(
        this HttpRequestData req, string message, string? correlationId = null)
    {
        var response = req.CreateResponse(HttpStatusCode.Unauthorized);
        response.Headers.Add("Content-Type", "application/json");
        await response.WriteStringAsync(
            JsonSerializer.Serialize(
                ApiError.From(message, errorCode: "UNAUTHORIZED", correlationId: correlationId),
                SharedJsonOptions.CamelCase));
        return response;
    }

    /// <summary>
    /// 403 Forbidden — authenticated but not authorised for this operation.
    /// </summary>
    public static async Task<HttpResponseData> ForbiddenAsync(
        this HttpRequestData req, string message, string? correlationId = null)
    {
        var response = req.CreateResponse(HttpStatusCode.Forbidden);
        response.Headers.Add("Content-Type", "application/json");
        await response.WriteStringAsync(
            JsonSerializer.Serialize(
                ApiError.From(message, errorCode: "FORBIDDEN", correlationId: correlationId),
                SharedJsonOptions.CamelCase));
        return response;
    }

    // -------------------------------------------------------------------------
    // 5xx Server Errors
    // -------------------------------------------------------------------------

    /// <summary>
    /// 500 Internal Server Error — an unexpected server-side fault.
    /// The <paramref name="correlationId"/> should always be included so
    /// on-call engineers can locate the corresponding log entry.
    /// Avoid leaking internal exception messages to callers; log them instead.
    /// </summary>
    public static async Task<HttpResponseData> InternalServerErrorAsync(
        this HttpRequestData req, string? correlationId = null)
    {
        var response = req.CreateResponse(HttpStatusCode.InternalServerError);
        response.Headers.Add("Content-Type", "application/json");
        await response.WriteStringAsync(
            JsonSerializer.Serialize(
                ApiError.From(
                    "An unexpected error occurred. Please retry or contact support.",
                    errorCode: "INTERNAL_ERROR",
                    correlationId: correlationId),
                SharedJsonOptions.CamelCase));
        return response;
    }
}

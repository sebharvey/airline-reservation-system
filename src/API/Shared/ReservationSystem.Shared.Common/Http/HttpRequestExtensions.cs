using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using ReservationSystem.Shared.Common.Json;
using System.Text.Json;

namespace ReservationSystem.Shared.Common.Http;

/// <summary>
/// Extension methods for <see cref="HttpRequestData"/> to simplify reading and
/// deserialising request bodies in Azure Functions.
/// </summary>
public static class HttpRequestExtensions
{
    /// <summary>
    /// Deserialises the JSON request body as <typeparamref name="T"/>.
    ///
    /// Returns <c>(value, null)</c> on success. Returns <c>(null, errorResponse)</c>
    /// if the body is absent, empty, or contains malformed JSON — the caller should
    /// return the error response immediately:
    ///
    /// <code>
    ///   var (request, error) = await req.TryDeserializeBodyAsync&lt;CreateOrderRequest&gt;(_logger, cancellationToken);
    ///   if (error is not null) return error;
    /// </code>
    ///
    /// A <see cref="System.Text.Json.JsonException"/> is logged as a warning and
    /// surfaces as a 400 Bad Request with message <c>"Invalid JSON in request body."</c>.
    /// A null result (empty body) surfaces as a 400 Bad Request with message
    /// <c>"Request body is required."</c>.
    /// </summary>
    public static async Task<(T? Value, HttpResponseData? Error)> TryDeserializeBodyAsync<T>(
        this HttpRequestData req,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        T? value;

        try
        {
            value = await JsonSerializer.DeserializeAsync<T>(
                req.Body, SharedJsonOptions.CamelCase, cancellationToken);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Invalid JSON in request body");
            return (default, await req.BadRequestAsync("Invalid JSON in request body."));
        }

        if (value is null)
            return (default, await req.BadRequestAsync("Request body is required."));

        return (value, null);
    }
}

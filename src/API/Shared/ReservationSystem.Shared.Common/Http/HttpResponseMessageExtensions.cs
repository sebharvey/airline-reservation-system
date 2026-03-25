using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ReservationSystem.Shared.Common.Http;

/// <summary>
/// Extension methods for <see cref="HttpResponseMessage"/> to simplify reading
/// error responses from downstream microservices in orchestration APIs.
///
/// Usage pattern — call on the response from an HttpClient call:
/// <code>
///   var message = await response.ReadErrorMessageAsync(cancellationToken);
///   throw new ArgumentException(message);
/// </code>
/// </summary>
public static class HttpResponseMessageExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Reads the error message from a non-2xx <see cref="HttpResponseMessage"/>.
    /// Attempts to deserialise the body as an <see cref="ApiError"/> envelope first;
    /// falls back to the raw response body string if that fails.
    /// </summary>
    public static async Task<string> ReadErrorMessageAsync(
        this HttpResponseMessage response,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var apiError = await response.Content.ReadFromJsonAsync<ApiError>(JsonOptions, cancellationToken);
            if (!string.IsNullOrWhiteSpace(apiError?.Error))
                return apiError.Error;
        }
        catch
        {
            // Fall through to raw body read
        }

        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(raw) ? "Validation failed." : raw;
    }
}

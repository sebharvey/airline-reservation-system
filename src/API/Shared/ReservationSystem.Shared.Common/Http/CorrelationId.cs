using Microsoft.Azure.Functions.Worker.Http;

namespace ReservationSystem.Shared.Common.Http;

/// <summary>
/// Utilities for reading and propagating the <c>X-Correlation-ID</c> request header.
///
/// Every inbound HTTP request should carry this header so that a single logical
/// operation can be traced end-to-end across the Retail API, microservices, and
/// any downstream dependencies.  If a caller omits it, a new GUID is generated
/// so all log lines within that invocation are still correlated.
///
/// Recommended function handler pattern:
/// <code>
///   public async Task&lt;HttpResponseData&gt; Run(HttpRequestData req)
///   {
///       var correlationId = CorrelationId.GetOrGenerate(req);
///       _logger.LogInformation("Handling request {CorrelationId}", correlationId);
///
///       var response = await req.OkJsonAsync(result);
///       CorrelationId.Propagate(response, correlationId);
///       return response;
///   }
/// </code>
/// </summary>
public static class CorrelationId
{
    /// <summary>
    /// The canonical header name sent by clients and propagated in responses.
    /// Use this constant everywhere rather than hard-coding the string.
    /// </summary>
    public const string HeaderName = "X-Correlation-ID";

    /// <summary>
    /// Returns the <c>X-Correlation-ID</c> from the request headers, or
    /// generates a fresh <see cref="Guid"/> string if the header is absent.
    ///
    /// Call once at the top of each function handler and pass the returned
    /// value down through handlers and into log calls.
    /// </summary>
    public static string GetOrGenerate(HttpRequestData req)
    {
        if (req.Headers.TryGetValues(HeaderName, out var values))
        {
            var id = values.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(id))
                return id;
        }

        return Guid.NewGuid().ToString();
    }

    /// <summary>
    /// Adds the <c>X-Correlation-ID</c> header to <paramref name="response"/>
    /// so the calling client can log or re-submit it in follow-up requests.
    ///
    /// Call this before returning any <see cref="HttpResponseData"/> — including
    /// error responses — so the header is present on every reply.
    /// </summary>
    public static void Propagate(HttpResponseData response, string correlationId)
        => response.Headers.Add(HeaderName, correlationId);
}

using Microsoft.Azure.Functions.Worker.Http;

namespace ReservationSystem.Shared.Common.Http;

/// <summary>
/// Utilities for reading the <c>Idempotency-Key</c> request header on
/// state-mutating endpoints.
///
/// Architecture requirement: all POST operations that create resources MUST
/// support idempotency so that safely-retried requests (network failures,
/// client timeouts) do not create duplicate records.
///
/// How it works:
/// 1. Client generates a stable UUID for the operation (e.g. "create order for
///    basket abc-123") and sends it as <c>Idempotency-Key: &lt;uuid&gt;</c>.
/// 2. The handler calls <see cref="TryGet"/> to extract the key.
/// 3. Before executing business logic the handler checks a short-lived cache
///    (e.g. an <c>IdempotencyKeys</c> table or distributed cache) to see if
///    this key was already processed.  If so, it returns the cached response.
/// 4. On success the handler persists the key and the response so future
///    duplicates are handled without re-running the operation.
///
/// Recommended handler pattern for POST endpoints:
/// <code>
///   public async Task&lt;HttpResponseData&gt; Create(HttpRequestData req)
///   {
///       var correlationId  = CorrelationId.GetOrGenerate(req);
///
///       if (!IdempotencyKey.TryGet(req, out var idempotencyKey))
///           return await req.BadRequestAsync(
///               "Idempotency-Key header is required.", correlationId);
///
///       // check cache / execute / cache result …
///   }
/// </code>
/// </summary>
public static class IdempotencyKey
{
    /// <summary>
    /// The canonical header name.  Use this constant rather than the raw string.
    /// </summary>
    public const string HeaderName = "Idempotency-Key";

    /// <summary>
    /// Attempts to read the <c>Idempotency-Key</c> header value from the request.
    /// </summary>
    /// <param name="req">The inbound Azure Functions HTTP request.</param>
    /// <param name="key">
    /// When this method returns <see langword="true"/>, contains the key value;
    /// otherwise <see langword="null"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the header is present and non-empty;
    /// <see langword="false"/> otherwise.
    /// </returns>
    public static bool TryGet(HttpRequestData req, out string? key)
    {
        if (req.Headers.TryGetValues(HeaderName, out var values))
        {
            var value = values.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(value))
            {
                key = value;
                return true;
            }
        }

        key = null;
        return false;
    }

    /// <summary>
    /// Returns the <c>Idempotency-Key</c> header value, or <see langword="null"/>
    /// if it is absent.  Use <see cref="TryGet"/> when the key is mandatory.
    /// </summary>
    public static string? TryGet(HttpRequestData req)
        => TryGet(req, out var key) ? key : null;
}

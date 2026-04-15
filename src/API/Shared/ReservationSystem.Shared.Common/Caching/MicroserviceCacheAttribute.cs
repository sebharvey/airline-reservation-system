namespace ReservationSystem.Shared.Common.Caching;

/// <summary>
/// Enables in-memory response caching for an Azure Function HTTP endpoint.
///
/// Place this attribute on a function method to cache its HTTP response for the
/// specified duration. The cache is keyed by HTTP method and full request URL,
/// so parameterised routes and query-string variants each get their own entry.
///
/// Only GET responses with a 2xx status code are cached. All other HTTP methods
/// and non-success responses pass through uncached. Functions without this
/// attribute are never cached — opt-in is explicit.
///
/// The <paramref name="cacheName"/> groups related cache entries under a logical
/// name (e.g. <c>"FareLogics"</c>, <c>"Schedule"</c>). This name is stored on
/// the attribute so that future cache-invalidation mechanisms can target all
/// entries belonging to the same named group without knowing individual cache keys.
///
/// <code>
///   [Function("GetSchedules")]
///   [MicroserviceCache("Schedule", 1)]   // cache for 1 hour
///   public async Task&lt;HttpResponseData&gt; GetSchedules(
///       [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/schedules")] HttpRequestData req,
///       CancellationToken cancellationToken) { ... }
/// </code>
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class MicroserviceCacheAttribute : Attribute
{
    /// <summary>
    /// Logical name for this cache group (e.g. <c>"FareLogics"</c>, <c>"Schedule"</c>).
    /// Used to identify and invalidate related cache entries on demand.
    /// </summary>
    public string CacheName { get; }

    /// <summary>
    /// How long a cached response is retained, in hours.
    /// Must be greater than zero.
    /// </summary>
    public int Hours { get; }

    /// <param name="cacheName">
    /// Logical name for the cache group (e.g. <c>"FareLogics"</c>).
    /// Must not be null or whitespace.
    /// </param>
    /// <param name="hours">
    /// Cache duration in hours (e.g. <c>1</c> for one hour, <c>24</c> for one day).
    /// Must be greater than zero.
    /// </param>
    public MicroserviceCacheAttribute(string cacheName, int hours)
    {
        if (string.IsNullOrWhiteSpace(cacheName))
            throw new ArgumentException("Cache name must not be null or whitespace.", nameof(cacheName));

        if (hours <= 0)
            throw new ArgumentOutOfRangeException(nameof(hours), "Cache duration must be greater than zero.");

        CacheName = cacheName;
        Hours = hours;
    }
}

namespace ReservationSystem.Shared.Common.Caching;

/// <summary>
/// Triggers invalidation of one or more named cache groups when an Azure Function
/// endpoint executes successfully.
///
/// Place this attribute on non-GET function methods (POST, PUT, PATCH, DELETE)
/// that write new data. After the function returns a 2xx response, the
/// middleware evicts every cached entry belonging to each named group, so the
/// next GET request repopulates the cache with fresh data.
///
/// Each name must match the name used on the corresponding
/// <see cref="MicroserviceCacheAttribute"/> decoration. Multiple names can be
/// supplied when a single write invalidates more than one cache group.
///
/// <code>
///   // Invalidate a single group
///   [Function("CreateBagPolicy")]
///   [MicroserviceCacheInvalidate("BagPolicy")]
///   public async Task&lt;HttpResponseData&gt; Create(...) { ... }
///
///   // Invalidate multiple groups — bag offers are derived from policy + pricing
///   [Function("UpdateBagPricing")]
///   [MicroserviceCacheInvalidate("BagPricing", "BagOffer")]
///   public async Task&lt;HttpResponseData&gt; Update(...) { ... }
/// </code>
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class MicroserviceCacheInvalidateAttribute : Attribute
{
    /// <summary>
    /// The logical cache group names to invalidate (e.g. <c>"BagPolicy"</c>,
    /// <c>"BagOffer"</c>). Each must match the <c>CacheName</c> on the
    /// corresponding <see cref="MicroserviceCacheAttribute"/>.
    /// </summary>
    public string[] CacheNames { get; }

    /// <param name="cacheNames">
    /// One or more cache group names to evict (e.g. <c>"BagPricing"</c>,
    /// <c>"BagOffer"</c>). Must not be null, empty, or contain whitespace entries.
    /// </param>
    public MicroserviceCacheInvalidateAttribute(params string[] cacheNames)
    {
        if (cacheNames is null || cacheNames.Length == 0)
            throw new ArgumentException("At least one cache name must be specified.", nameof(cacheNames));

        if (cacheNames.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Cache names must not contain null or whitespace entries.", nameof(cacheNames));

        CacheNames = cacheNames;
    }
}

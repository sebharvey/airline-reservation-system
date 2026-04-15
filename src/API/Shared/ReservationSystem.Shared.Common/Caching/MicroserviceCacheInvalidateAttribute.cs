namespace ReservationSystem.Shared.Common.Caching;

/// <summary>
/// Triggers invalidation of a named cache group when an Azure Function
/// endpoint executes successfully.
///
/// Place this attribute on non-GET function methods (POST, PUT, PATCH, DELETE)
/// that write new data. After the function returns a 2xx response, the
/// middleware evicts every cached entry belonging to the named group, so the
/// next GET request repopulates the cache with fresh data.
///
/// The <paramref name="cacheName"/> must match the name used on the
/// corresponding <see cref="MicroserviceCacheAttribute"/> decoration.
///
/// <code>
///   [Function("CreateBagPolicy")]
///   [MicroserviceCacheInvalidate("BagPolicy")]
///   public async Task&lt;HttpResponseData&gt; CreateBagPolicy(
///       [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/bags/policies")] HttpRequestData req,
///       CancellationToken cancellationToken) { ... }
/// </code>
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class MicroserviceCacheInvalidateAttribute : Attribute
{
    /// <summary>
    /// The logical cache group to invalidate (e.g. <c>"BagPolicy"</c>).
    /// Must match the <c>CacheName</c> on the target <see cref="MicroserviceCacheAttribute"/>.
    /// </summary>
    public string CacheName { get; }

    /// <param name="cacheName">
    /// The cache group name to evict (e.g. <c>"BagPolicy"</c>).
    /// Must not be null or whitespace.
    /// </param>
    public MicroserviceCacheInvalidateAttribute(string cacheName)
    {
        if (string.IsNullOrWhiteSpace(cacheName))
            throw new ArgumentException("Cache name must not be null or whitespace.", nameof(cacheName));

        CacheName = cacheName;
    }
}

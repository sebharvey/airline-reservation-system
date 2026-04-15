using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Net;
using System.Reflection;

namespace ReservationSystem.Shared.Common.Caching;

/// <summary>
/// Azure Functions isolated-worker middleware that serves cached HTTP responses
/// for endpoints decorated with <see cref="MicroserviceCacheAttribute"/>, and
/// invalidates named cache groups for endpoints decorated with
/// <see cref="MicroserviceCacheInvalidateAttribute"/>.
///
/// Pipeline behaviour:
/// <list type="bullet">
///   <item>Functions without either attribute are passed through immediately — zero overhead.</item>
///   <item>GET requests decorated with <see cref="MicroserviceCacheAttribute"/>: cache hit
///         short-circuits the function; cache miss executes the function and stores the 2xx
///         response body, keyed under the named group.</item>
///   <item>Non-GET requests decorated with <see cref="MicroserviceCacheInvalidateAttribute"/>:
///         after a successful (2xx) response all cache entries registered under the named
///         group are evicted, so the next GET repopulates from the source of truth.</item>
///   <item>Non-2xx responses are never cached and never trigger invalidation.</item>
/// </list>
///
/// Register via <c>worker.UseMicroserviceCache()</c> and
/// <c>services.AddMicroserviceCache()</c> in <c>Program.cs</c>.
/// </summary>
public sealed class MicroserviceCacheMiddleware : IFunctionsWorkerMiddleware
{
    // Both cache attributes are resolved together in a single reflection pass per
    // function entry point and stored here so subsequent invocations are free.
    private readonly ConcurrentDictionary<string, CacheAttributes> _resolvedAttributes = new();

    // Tracks every cache key stored under a given cache name so that
    // invalidation can evict the whole group without knowing individual keys.
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _keysByName = new();

    private readonly IMemoryCache _cache;
    private readonly ILogger<MicroserviceCacheMiddleware> _logger;

    public MicroserviceCacheMiddleware(IMemoryCache cache, ILogger<MicroserviceCacheMiddleware> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var attrs = ResolveAttributes(context);

        // No attributes on this function — pass through with zero overhead.
        if (attrs.Cache is null && attrs.Invalidate is null)
        {
            await next(context);
            return;
        }

        var req = await context.GetHttpRequestDataAsync();
        var isGet = req is not null && req.Method.Equals("GET", StringComparison.OrdinalIgnoreCase);

        // ── Cache hit short-circuit (GET + MicroserviceCacheAttribute) ─────────
        if (attrs.Cache is not null && isGet && req is not null)
        {
            var cacheKey = BuildCacheKey(context, req, attrs.Cache);

            if (_cache.TryGetValue<CachedHttpResponse>(cacheKey, out var cached) && cached is not null)
            {
                _logger.LogDebug("Cache hit for {CacheKey}", cacheKey);

                var hit = req.CreateResponse(cached.StatusCode);

                foreach (var (name, values) in cached.Headers)
                    hit.Headers.TryAddWithoutValidation(name, values);

                await hit.Body.WriteAsync(cached.Body);

                context.GetInvocationResult().Value = hit;
                return; // Short-circuit — function body never executes.
            }
        }

        // ── Execute function ───────────────────────────────────────────────────
        await next(context);

        if (req is null) return;

        var response = context.GetInvocationResult().Value as HttpResponseData;
        var statusCode = response is not null ? (int)response.StatusCode : 0;
        var isSuccess = statusCode >= 200 && statusCode < 300;

        // ── Populate cache after successful GET ────────────────────────────────
        if (attrs.Cache is not null && isGet && isSuccess && response is not null)
        {
            if (response.Body.CanSeek)
            {
                response.Body.Position = 0;
                using var buffer = new MemoryStream();
                await response.Body.CopyToAsync(buffer);
                var bodyBytes = buffer.ToArray();
                response.Body.Position = 0;

                var headers = response.Headers
                    .ToDictionary(h => h.Key, h => h.Value.ToArray(), StringComparer.OrdinalIgnoreCase);

                var cacheKey = BuildCacheKey(context, req, attrs.Cache);
                var entry = new CachedHttpResponse(response.StatusCode, headers, bodyBytes);
                _cache.Set(cacheKey, entry, TimeSpan.FromHours(attrs.Cache.Hours));

                // Register the key under its cache name so invalidation can find it.
                _keysByName
                    .GetOrAdd(attrs.Cache.CacheName, _ => new ConcurrentDictionary<string, byte>(StringComparer.Ordinal))
                    .TryAdd(cacheKey, 0);

                _logger.LogDebug("Cached response for {CacheKey} (name={CacheName}, {Hours}h TTL)",
                    cacheKey, attrs.Cache.CacheName, attrs.Cache.Hours);
            }
        }

        // ── Invalidate cache after successful non-GET ──────────────────────────
        if (attrs.Invalidate is not null && !isGet && isSuccess)
        {
            foreach (var name in attrs.Invalidate.CacheNames)
            {
                if (_keysByName.TryRemove(name, out var keys))
                {
                    foreach (var key in keys.Keys)
                        _cache.Remove(key);

                    _logger.LogDebug("Invalidated {Count} cache entries for name={CacheName}",
                        keys.Count, name);
                }
            }
        }
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    /// <summary>
    /// Resolves both cache attributes for the function being invoked in a single
    /// reflection pass. The result is stored per entry point so reflection only
    /// runs once per unique function across all invocations.
    /// </summary>
    private CacheAttributes ResolveAttributes(FunctionContext context)
    {
        var entryPoint = context.FunctionDefinition.EntryPoint;

        return _resolvedAttributes.GetOrAdd(entryPoint, static ep =>
        {
            var lastDot = ep.LastIndexOf('.');
            if (lastDot < 0) return default;

            var typeName   = ep[..lastDot];
            var methodName = ep[(lastDot + 1)..];

            // Scan loaded assemblies for the declaring type. This runs once per
            // function; the result is stored in _resolvedAttributes thereafter.
            var type = AppDomain.CurrentDomain
                .GetAssemblies()
                .SelectMany(a =>
                {
                    try   { return a.GetTypes(); }
                    catch { return Array.Empty<Type>(); }
                })
                .FirstOrDefault(t => t.FullName == typeName);

            var method = type?.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);

            return new CacheAttributes(
                method?.GetCustomAttribute<MicroserviceCacheAttribute>(),
                method?.GetCustomAttribute<MicroserviceCacheInvalidateAttribute>());
        });
    }

    private static string BuildCacheKey(FunctionContext context, HttpRequestData req, MicroserviceCacheAttribute attribute)
        => $"{attribute.CacheName}:{context.FunctionDefinition.EntryPoint}:{req.Method.ToUpperInvariant()}:{req.Url.AbsoluteUri}";
}

/// <summary>
/// Pair of optional cache attributes resolved once per function entry point.
/// </summary>
internal readonly record struct CacheAttributes(
    MicroserviceCacheAttribute? Cache,
    MicroserviceCacheInvalidateAttribute? Invalidate);

/// <summary>
/// Immutable snapshot of an HTTP response stored in the memory cache.
/// </summary>
internal sealed record CachedHttpResponse(
    HttpStatusCode StatusCode,
    IReadOnlyDictionary<string, string[]> Headers,
    byte[] Body);

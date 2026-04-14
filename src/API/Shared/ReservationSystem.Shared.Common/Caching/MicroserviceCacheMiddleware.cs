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
/// for endpoints decorated with <see cref="MicroserviceCacheAttribute"/>.
///
/// Pipeline behaviour:
/// <list type="bullet">
///   <item>Functions without <see cref="MicroserviceCacheAttribute"/> are passed
///         through immediately — zero overhead.</item>
///   <item>Non-GET requests on decorated functions are passed through uncached
///         (mutating verbs must never be cached).</item>
///   <item>On a cache hit the downstream function and all DB calls are bypassed;
///         the stored response bytes are written directly to the invocation result.</item>
///   <item>On a cache miss the function executes normally. If the response is 2xx
///         the body is read, stored in <see cref="IMemoryCache"/>, and the stream
///         is rewound so the runtime can still send it to the caller.</item>
///   <item>Non-2xx responses (errors) are never cached.</item>
/// </list>
///
/// Register via <c>worker.UseMicroserviceCache()</c> and
/// <c>services.AddMicroserviceCache()</c> in <c>Program.cs</c>.
/// </summary>
public sealed class MicroserviceCacheMiddleware : IFunctionsWorkerMiddleware
{
    // Caches the result of reflection per function entry point so that the
    // assembly scan only happens once per unique function across all invocations.
    private readonly ConcurrentDictionary<string, MicroserviceCacheAttribute?> _attributeCache = new();

    private readonly IMemoryCache _cache;
    private readonly ILogger<MicroserviceCacheMiddleware> _logger;

    public MicroserviceCacheMiddleware(IMemoryCache cache, ILogger<MicroserviceCacheMiddleware> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task InvokeAsync(FunctionContext context, FunctionExecutionDelegate next)
    {
        var attribute = ResolveAttribute(context);

        // No attribute — pass through with zero overhead.
        if (attribute is null)
        {
            await next(context);
            return;
        }

        var req = await context.GetHttpRequestDataAsync();

        // Only cache safe, idempotent GET requests.
        if (req is null || !req.Method.Equals("GET", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        var cacheKey = BuildCacheKey(context, req);

        // ── Cache hit ──────────────────────────────────────────────────────────
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

        // ── Cache miss — execute function ──────────────────────────────────────
        await next(context);

        if (context.GetInvocationResult().Value is not HttpResponseData response)
            return;

        // Only cache successful responses.
        var statusCode = (int)response.StatusCode;
        if (statusCode < 200 || statusCode >= 300)
            return;

        if (!response.Body.CanSeek)
            return;

        // Read the body, store it, then rewind so the runtime can still send it.
        response.Body.Position = 0;
        using var buffer = new MemoryStream();
        await response.Body.CopyToAsync(buffer);
        var bodyBytes = buffer.ToArray();
        response.Body.Position = 0;

        var headers = response.Headers
            .ToDictionary(h => h.Key, h => h.Value.ToArray(), StringComparer.OrdinalIgnoreCase);

        var entry = new CachedHttpResponse(response.StatusCode, headers, bodyBytes);
        _cache.Set(cacheKey, entry, TimeSpan.FromHours(attribute.Hours));

        _logger.LogDebug("Cached response for {CacheKey} ({Hours}h TTL)", cacheKey, attribute.Hours);
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    /// <summary>
    /// Resolves the <see cref="MicroserviceCacheAttribute"/> for the function being
    /// invoked. The result is cached in <see cref="_attributeCache"/> so reflection
    /// only runs once per unique entry point across all invocations.
    /// </summary>
    private MicroserviceCacheAttribute? ResolveAttribute(FunctionContext context)
    {
        var entryPoint = context.FunctionDefinition.EntryPoint;

        return _attributeCache.GetOrAdd(entryPoint, static ep =>
        {
            var lastDot = ep.LastIndexOf('.');
            if (lastDot < 0) return null;

            var typeName  = ep[..lastDot];
            var methodName = ep[(lastDot + 1)..];

            // Scan loaded assemblies for the declaring type. This runs once per
            // function; the result is stored in _attributeCache thereafter.
            var type = AppDomain.CurrentDomain
                .GetAssemblies()
                .SelectMany(a =>
                {
                    try   { return a.GetTypes(); }
                    catch { return Array.Empty<Type>(); }
                })
                .FirstOrDefault(t => t.FullName == typeName);

            return type
                ?.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance)
                ?.GetCustomAttribute<MicroserviceCacheAttribute>();
        });
    }

    private static string BuildCacheKey(FunctionContext context, HttpRequestData req)
        => $"{context.FunctionDefinition.EntryPoint}:{req.Method.ToUpperInvariant()}:{req.Url.AbsoluteUri}";
}

/// <summary>
/// Immutable snapshot of an HTTP response stored in the memory cache.
/// </summary>
internal sealed record CachedHttpResponse(
    HttpStatusCode StatusCode,
    IReadOnlyDictionary<string, string[]> Headers,
    byte[] Body);

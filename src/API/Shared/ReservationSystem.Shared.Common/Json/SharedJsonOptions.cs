using System.Text.Json;
using System.Text.Json.Serialization;

namespace ReservationSystem.Shared.Common.Json;

/// <summary>
/// Shared JSON serializer options used consistently across all APIs.
/// </summary>
public static class SharedJsonOptions
{
    /// <summary>
    /// Standard camelCase serializer options for HTTP request/response bodies
    /// and internal JSON serialisation (e.g. database JSON columns).
    /// </summary>
    public static readonly JsonSerializerOptions CamelCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// camelCase serializer options that also omits null-valued properties from
    /// the output. Use for outbound HTTP calls to downstream services where the
    /// receiver should treat absent and null fields identically.
    /// </summary>
    public static readonly JsonSerializerOptions CamelCaseIgnoreNull = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}

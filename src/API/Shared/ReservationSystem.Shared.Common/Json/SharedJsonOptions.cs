using System.Text.Json;

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
}

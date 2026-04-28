using ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices.Dto;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices;

public sealed class SeatServiceClient
{
    private readonly HttpClient _httpClient;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public SeatServiceClient(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("AncillaryMs");
    }

    /// <summary>
    /// Retrieves all aircraft types from the Seat MS GET /v1/aircraft-types endpoint.
    /// </summary>
    public async Task<GetAircraftTypesDto> GetAircraftTypesAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync("/api/v1/aircraft-types", cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<GetAircraftTypesDto>(JsonOptions, cancellationToken);
        return result ?? throw new InvalidOperationException("Empty response from Seat MS get aircraft types.");
    }

    /// <summary>
    /// Retrieves cabin layout configs (columns, row range) for each cabin from the active seatmap
    /// for the given aircraft type. Returns null when no seatmap exists for the aircraft type.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, SeatCabinConfigDto>?> GetSeatmapCabinConfigsAsync(
        string aircraftType, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync(
            $"/api/v1/seatmap/{Uri.EscapeDataString(aircraftType)}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        // The Ancillary MS returns the raw CabinLayout JSON as the "cabins" property.
        // CabinLayout may be stored as a top-level array of cabin objects, or as an object
        // with a nested "cabins" array (the seed-data format). Handle both.
        if (!root.TryGetProperty("cabins", out var cabinsOuter))
            return null;

        JsonElement cabinsArray;
        if (cabinsOuter.ValueKind == JsonValueKind.Array)
        {
            cabinsArray = cabinsOuter;
        }
        else if (cabinsOuter.ValueKind == JsonValueKind.Object &&
                 cabinsOuter.TryGetProperty("cabins", out var inner) &&
                 inner.ValueKind == JsonValueKind.Array)
        {
            cabinsArray = inner;
        }
        else
        {
            return null;
        }

        var configs = new Dictionary<string, SeatCabinConfigDto>(StringComparer.OrdinalIgnoreCase);
        foreach (var cabin in cabinsArray.EnumerateArray())
        {
            var cabinCode = cabin.TryGetProperty("cabinCode", out var cc) ? cc.GetString() : null;
            if (string.IsNullOrWhiteSpace(cabinCode)) continue;

            if (!cabin.TryGetProperty("startRow", out var srEl) ||
                !cabin.TryGetProperty("endRow",   out var erEl)) continue;

            var startRow = srEl.GetInt32();
            var endRow   = erEl.GetInt32();

            if (startRow <= 0 || endRow < startRow) continue;

            var cols = new List<string>();
            if (cabin.TryGetProperty("columns", out var colsEl) && colsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var col in colsEl.EnumerateArray())
                {
                    var c = col.GetString();
                    if (c is not null) cols.Add(c);
                }
            }

            if (cols.Count > 0)
                configs[cabinCode] = new SeatCabinConfigDto(cols, startRow, endRow);
        }

        return configs.Count > 0 ? configs : null;
    }
}

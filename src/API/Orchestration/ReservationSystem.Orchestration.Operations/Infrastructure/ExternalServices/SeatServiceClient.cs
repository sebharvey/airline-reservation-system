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
    /// Retrieves the full seatmap layout including all rows and individual seats for an aircraft type.
    /// Returns null when no active seatmap exists.
    /// </summary>
    public async Task<FullSeatmapDto?> GetFullSeatmapAsync(
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

        var aircraftTypeValue = root.TryGetProperty("aircraftType", out var atProp) ? atProp.GetString() ?? aircraftType : aircraftType;

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

        var cabins = new List<FullCabinLayoutDto>();
        foreach (var cabinEl in cabinsArray.EnumerateArray())
        {
            var cabinCode = cabinEl.TryGetProperty("cabinCode", out var cc) ? cc.GetString() ?? string.Empty : string.Empty;
            var cabinName = cabinEl.TryGetProperty("cabinName", out var cn) ? cn.GetString() ?? string.Empty : string.Empty;
            var startRow  = cabinEl.TryGetProperty("startRow", out var sr) ? sr.GetInt32() : 0;
            var endRow    = cabinEl.TryGetProperty("endRow",   out var er) ? er.GetInt32() : 0;
            var layout    = cabinEl.TryGetProperty("layout",   out var lay) ? lay.GetString() ?? string.Empty : string.Empty;

            var columns = new List<string>();
            if (cabinEl.TryGetProperty("columns", out var colsEl) && colsEl.ValueKind == JsonValueKind.Array)
                foreach (var col in colsEl.EnumerateArray())
                {
                    var c = col.GetString();
                    if (c is not null) columns.Add(c);
                }

            var rows = new List<FullRowLayoutDto>();
            if (cabinEl.TryGetProperty("rows", out var rowsEl) && rowsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var rowEl in rowsEl.EnumerateArray())
                {
                    var rowNumber = rowEl.TryGetProperty("rowNumber", out var rn) ? rn.GetInt32() : 0;
                    var seats = new List<FullSeatLayoutDto>();

                    if (rowEl.TryGetProperty("seats", out var seatsEl) && seatsEl.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var seatEl in seatsEl.EnumerateArray())
                        {
                            var seatNumber   = seatEl.TryGetProperty("seatNumber",   out var sn)  ? sn.GetString()  ?? string.Empty : string.Empty;
                            var column       = seatEl.TryGetProperty("column",       out var scol) ? scol.GetString() ?? string.Empty : string.Empty;
                            var position     = seatEl.TryGetProperty("position",     out var pos)  ? pos.GetString()  ?? string.Empty : string.Empty;
                            var isSelectable = seatEl.TryGetProperty("isSelectable", out var sel)  && sel.GetBoolean();

                            var attrs = new List<string>();
                            if (seatEl.TryGetProperty("attributes", out var attrsEl) && attrsEl.ValueKind == JsonValueKind.Array)
                                foreach (var attr in attrsEl.EnumerateArray())
                                {
                                    var a = attr.GetString();
                                    if (a is not null) attrs.Add(a);
                                }

                            seats.Add(new FullSeatLayoutDto
                            {
                                SeatNumber   = seatNumber,
                                Column       = column,
                                Position     = position,
                                Attributes   = attrs,
                                IsSelectable = isSelectable
                            });
                        }
                    }
                    rows.Add(new FullRowLayoutDto { RowNumber = rowNumber, Seats = seats });
                }
            }

            cabins.Add(new FullCabinLayoutDto
            {
                CabinCode = cabinCode,
                CabinName = cabinName,
                StartRow  = startRow,
                EndRow    = endRow,
                Columns   = columns,
                Layout    = layout,
                Rows      = rows
            });
        }

        return new FullSeatmapDto { AircraftType = aircraftTypeValue, Cabins = cabins };
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

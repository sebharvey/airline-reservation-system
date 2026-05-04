using System.Text.Json.Serialization;

namespace ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices.Dto;

public sealed class FullSeatmapDto
{
    [JsonPropertyName("aircraftType")]
    public string AircraftType { get; init; } = string.Empty;

    [JsonPropertyName("cabins")]
    public IReadOnlyList<FullCabinLayoutDto> Cabins { get; init; } = [];
}

public sealed class FullCabinLayoutDto
{
    [JsonPropertyName("cabinCode")]
    public string CabinCode { get; init; } = string.Empty;

    [JsonPropertyName("cabinName")]
    public string CabinName { get; init; } = string.Empty;

    [JsonPropertyName("startRow")]
    public int StartRow { get; init; }

    [JsonPropertyName("endRow")]
    public int EndRow { get; init; }

    [JsonPropertyName("columns")]
    public IReadOnlyList<string> Columns { get; init; } = [];

    [JsonPropertyName("layout")]
    public string Layout { get; init; } = string.Empty;

    [JsonPropertyName("rows")]
    public IReadOnlyList<FullRowLayoutDto> Rows { get; init; } = [];
}

public sealed class FullRowLayoutDto
{
    [JsonPropertyName("rowNumber")]
    public int RowNumber { get; init; }

    [JsonPropertyName("seats")]
    public IReadOnlyList<FullSeatLayoutDto> Seats { get; init; } = [];
}

public sealed class FullSeatLayoutDto
{
    [JsonPropertyName("seatNumber")]
    public string SeatNumber { get; init; } = string.Empty;

    [JsonPropertyName("column")]
    public string Column { get; init; } = string.Empty;

    [JsonPropertyName("position")]
    public string Position { get; init; } = string.Empty;

    [JsonPropertyName("attributes")]
    public IReadOnlyList<string> Attributes { get; init; } = [];

    [JsonPropertyName("isSelectable")]
    public bool IsSelectable { get; init; }
}

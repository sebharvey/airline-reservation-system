namespace ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices.Dto;

public sealed record SeatmapLayoutDto(
    Guid SeatmapId,
    string AircraftType,
    int Version,
    int TotalSeats,
    List<CabinLayoutDto> Cabins);

public sealed record CabinLayoutDto(
    string CabinCode,
    string CabinName,
    string DeckLevel,
    int StartRow,
    int EndRow,
    List<string> Columns,
    string Layout,
    List<RowLayoutDto> Rows);

public sealed record RowLayoutDto(
    int RowNumber,
    List<SeatLayoutDto> Seats);

public sealed record SeatLayoutDto(
    string SeatNumber,
    string Column,
    string Type,
    string Position,
    List<string> Attributes,
    bool IsSelectable);

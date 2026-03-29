namespace ReservationSystem.Orchestration.Operations.Application.ImportSchedulesToInventory;

public sealed record ImportSchedulesToInventoryCommand(
    IReadOnlyList<AircraftConfig> AircraftConfigs,
    Guid? ScheduleGroupId = null);

public sealed record AircraftConfig(
    string AircraftTypeCode,
    IReadOnlyList<CabinSeatCount> Cabins);

public sealed record CabinSeatCount(
    string CabinCode,
    int TotalSeats);

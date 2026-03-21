namespace ReservationSystem.Orchestration.Operations.Models.Requests;

public sealed class CreateScheduleRequest
{
    public string FlightNumber { get; init; } = string.Empty;
    public string Origin { get; init; } = string.Empty;
    public string Destination { get; init; } = string.Empty;
    public TimeOnly DepartureTime { get; init; }
    public TimeOnly ArrivalTime { get; init; }
    public string AircraftType { get; init; } = string.Empty;
    public DateOnly EffectiveFrom { get; init; }
    public DateOnly? EffectiveTo { get; init; }
    public IReadOnlyList<DayOfWeek> OperatingDays { get; init; } = [];
}

namespace ReservationSystem.Microservices.Offer.Domain.ExternalServices;

/// <summary>
/// Retrieves flight schedules from the Schedule microservice.
/// </summary>
public interface IScheduleServiceClient
{
    Task<ScheduleData> GetSchedulesAsync(CancellationToken cancellationToken = default);
}

public sealed class ScheduleData
{
    public int Count { get; init; }
    public IReadOnlyList<ScheduleItem> Schedules { get; init; } = [];
}

public sealed class ScheduleItem
{
    public string FlightNumber { get; init; } = string.Empty;
    public string Origin { get; init; } = string.Empty;
    public string Destination { get; init; } = string.Empty;
    public string DepartureTime { get; init; } = string.Empty;
    public string ArrivalTime { get; init; } = string.Empty;
    public int ArrivalDayOffset { get; init; }
    public int DaysOfWeek { get; init; }
    public string AircraftType { get; init; } = string.Empty;
    public string ValidFrom { get; init; } = string.Empty;
    public string ValidTo { get; init; } = string.Empty;
}

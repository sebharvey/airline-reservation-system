namespace ReservationSystem.Microservices.Schedule.Domain.Entities;

/// <summary>
/// Core domain entity representing a flight schedule.
/// Defines the recurring pattern of flights for a given route and time window.
/// </summary>
public sealed class FlightSchedule
{
    public Guid ScheduleId { get; private set; }
    public string FlightNumber { get; private set; } = string.Empty;
    public string Origin { get; private set; } = string.Empty;
    public string Destination { get; private set; } = string.Empty;
    public TimeSpan DepartureTime { get; private set; }
    public TimeSpan ArrivalTime { get; private set; }
    public byte ArrivalDayOffset { get; private set; }
    public byte DaysOfWeek { get; private set; }
    public string AircraftType { get; private set; } = string.Empty;
    public DateTime ValidFrom { get; private set; }
    public DateTime ValidTo { get; private set; }
    public int FlightsCreated { get; private set; }
    public string CabinFares { get; private set; } = string.Empty;
    public string CreatedBy { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private FlightSchedule() { }

    /// <summary>
    /// Factory method for creating a brand-new flight schedule.
    /// </summary>
    public static FlightSchedule Create(
        string flightNumber,
        string origin,
        string destination,
        TimeSpan departureTime,
        TimeSpan arrivalTime,
        byte arrivalDayOffset,
        byte daysOfWeek,
        string aircraftType,
        DateTime validFrom,
        DateTime validTo,
        string cabinFares,
        string createdBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(flightNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(origin);
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);
        ArgumentException.ThrowIfNullOrWhiteSpace(aircraftType);
        ArgumentException.ThrowIfNullOrWhiteSpace(cabinFares);
        ArgumentException.ThrowIfNullOrWhiteSpace(createdBy);

        var now = DateTime.UtcNow;
        return new FlightSchedule
        {
            ScheduleId = Guid.NewGuid(),
            FlightNumber = flightNumber,
            Origin = origin,
            Destination = destination,
            DepartureTime = departureTime,
            ArrivalTime = arrivalTime,
            ArrivalDayOffset = arrivalDayOffset,
            DaysOfWeek = daysOfWeek,
            AircraftType = aircraftType,
            ValidFrom = validFrom,
            ValidTo = validTo,
            FlightsCreated = 0,
            CabinFares = cabinFares,
            CreatedBy = createdBy,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>
    /// Factory method for reconstituting from persistence.
    /// </summary>
    public static FlightSchedule Reconstitute(
        Guid scheduleId,
        string flightNumber,
        string origin,
        string destination,
        TimeSpan departureTime,
        TimeSpan arrivalTime,
        byte arrivalDayOffset,
        byte daysOfWeek,
        string aircraftType,
        DateTime validFrom,
        DateTime validTo,
        int flightsCreated,
        string cabinFares,
        string createdBy,
        DateTime createdAt,
        DateTime updatedAt)
    {
        return new FlightSchedule
        {
            ScheduleId = scheduleId,
            FlightNumber = flightNumber,
            Origin = origin,
            Destination = destination,
            DepartureTime = departureTime,
            ArrivalTime = arrivalTime,
            ArrivalDayOffset = arrivalDayOffset,
            DaysOfWeek = daysOfWeek,
            AircraftType = aircraftType,
            ValidFrom = validFrom,
            ValidTo = validTo,
            FlightsCreated = flightsCreated,
            CabinFares = cabinFares,
            CreatedBy = createdBy,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
    }

    public void UpdateFlightsCreated(int count)
    {
        FlightsCreated = count;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Returns the list of operating dates for this schedule based on the
    /// DaysOfWeek bitmask and ValidFrom/ValidTo range.
    /// </summary>
    public IReadOnlyList<DateTime> GetOperatingDates()
    {
        var dates = new List<DateTime>();
        for (var date = ValidFrom; date <= ValidTo; date = date.AddDays(1))
        {
            var dayBit = date.DayOfWeek switch
            {
                System.DayOfWeek.Monday => 1,
                System.DayOfWeek.Tuesday => 2,
                System.DayOfWeek.Wednesday => 4,
                System.DayOfWeek.Thursday => 8,
                System.DayOfWeek.Friday => 16,
                System.DayOfWeek.Saturday => 32,
                System.DayOfWeek.Sunday => 64,
                _ => 0
            };

            if ((DaysOfWeek & dayBit) != 0)
                dates.Add(date);
        }
        return dates.AsReadOnly();
    }
}

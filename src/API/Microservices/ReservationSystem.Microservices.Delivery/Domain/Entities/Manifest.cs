namespace ReservationSystem.Microservices.Delivery.Domain.Entities;

/// <summary>
/// Operational passenger manifest entry — one row per passenger per flight segment.
/// Maps to [delivery].[Manifest].
/// </summary>
public sealed class Manifest
{
    public Guid ManifestId { get; private set; }
    public Guid TicketId { get; private set; }
    public Guid InventoryId { get; private set; }
    public string FlightNumber { get; private set; } = string.Empty;
    public DateTime DepartureDate { get; private set; }
    public string AircraftType { get; private set; } = string.Empty;
    public string SeatNumber { get; private set; } = string.Empty;
    public string CabinCode { get; private set; } = string.Empty;
    public string BookingReference { get; private set; } = string.Empty;
    public string ETicketNumber { get; private set; } = string.Empty;
    public string PassengerId { get; private set; } = string.Empty;
    public string GivenName { get; private set; } = string.Empty;
    public string Surname { get; private set; } = string.Empty;
    public string? SsrCodes { get; private set; }
    public TimeSpan DepartureTime { get; private set; }
    public TimeSpan ArrivalTime { get; private set; }
    public bool CheckedIn { get; private set; }
    public DateTime? CheckedInAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public int Version { get; private set; }

    private Manifest() { }

    public static Manifest Create(
        Guid ticketId, Guid inventoryId, string flightNumber, DateTime departureDate,
        string aircraftType, string seatNumber, string cabinCode,
        string bookingReference, string eTicketNumber,
        string passengerId, string givenName, string surname,
        string? ssrCodes, TimeSpan departureTime, TimeSpan arrivalTime)
    {
        var now = DateTime.UtcNow;
        return new Manifest
        {
            ManifestId = Guid.NewGuid(),
            TicketId = ticketId,
            InventoryId = inventoryId,
            FlightNumber = flightNumber,
            DepartureDate = departureDate,
            AircraftType = aircraftType,
            SeatNumber = seatNumber,
            CabinCode = cabinCode,
            BookingReference = bookingReference,
            ETicketNumber = eTicketNumber,
            PassengerId = passengerId,
            GivenName = givenName,
            Surname = surname,
            SsrCodes = ssrCodes,
            DepartureTime = departureTime,
            ArrivalTime = arrivalTime,
            CheckedIn = false,
            CheckedInAt = null,
            CreatedAt = now,
            UpdatedAt = now,
            Version = 1
        };
    }

    public static Manifest Reconstitute(
        Guid manifestId, Guid ticketId, Guid inventoryId,
        string flightNumber, DateTime departureDate, string aircraftType,
        string seatNumber, string cabinCode, string bookingReference,
        string eTicketNumber, string passengerId, string givenName,
        string surname, string? ssrCodes, TimeSpan departureTime,
        TimeSpan arrivalTime, bool checkedIn, DateTime? checkedInAt,
        DateTime createdAt, DateTime updatedAt, int version)
    {
        return new Manifest
        {
            ManifestId = manifestId,
            TicketId = ticketId,
            InventoryId = inventoryId,
            FlightNumber = flightNumber,
            DepartureDate = departureDate,
            AircraftType = aircraftType,
            SeatNumber = seatNumber,
            CabinCode = cabinCode,
            BookingReference = bookingReference,
            ETicketNumber = eTicketNumber,
            PassengerId = passengerId,
            GivenName = givenName,
            Surname = surname,
            SsrCodes = ssrCodes,
            DepartureTime = departureTime,
            ArrivalTime = arrivalTime,
            CheckedIn = checkedIn,
            CheckedInAt = checkedInAt,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            Version = version
        };
    }

    public void UpdateSeat(string seatNumber, string eTicketNumber)
    {
        SeatNumber = seatNumber;
        ETicketNumber = eTicketNumber;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateCheckIn(bool checkedIn, DateTime? checkedInAt)
    {
        CheckedIn = checkedIn;
        CheckedInAt = checkedInAt;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateSsrCodes(string? ssrCodes)
    {
        SsrCodes = ssrCodes;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateFlightTimes(TimeSpan departureTime, TimeSpan arrivalTime)
    {
        DepartureTime = departureTime;
        ArrivalTime = arrivalTime;
        UpdatedAt = DateTime.UtcNow;
    }
}

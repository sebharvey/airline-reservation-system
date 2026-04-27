namespace ReservationSystem.Microservices.Delivery.Domain.Entities;

/// <summary>
/// Passenger manifest entry — one row per passenger per flight segment.
/// Written at booking confirmation; updated at check-in.
/// </summary>
public sealed class Manifest
{
    public Guid ManifestId { get; private set; }
    public Guid TicketId { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid InventoryId { get; private set; }
    public string FlightNumber { get; private set; } = string.Empty;
    public string Origin { get; private set; } = string.Empty;
    public string Destination { get; private set; } = string.Empty;
    public DateOnly DepartureDate { get; private set; }
    public string AircraftType { get; private set; } = string.Empty;
    public string SeatNumber { get; private set; } = string.Empty;
    public string CabinCode { get; private set; } = string.Empty;
    public string BookingReference { get; private set; } = string.Empty;
    public string ETicketNumber { get; private set; } = string.Empty;
    public string PassengerId { get; private set; } = string.Empty;
    public string GivenName { get; private set; } = string.Empty;
    public string Surname { get; private set; } = string.Empty;
    public string? SsrCodes { get; private set; }
    public string BookingType { get; private set; } = string.Empty;
    public TimeOnly DepartureTime { get; private set; }
    public TimeOnly ArrivalTime { get; private set; }
    public bool CheckedIn { get; private set; }
    public DateTime? CheckedInAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public int Version { get; private set; }

    private Manifest() { }

    public void Rebook(
        Guid newInventoryId,
        Guid newTicketId,
        string newFlightNumber,
        string newOrigin,
        string newDestination,
        DateOnly newDepartureDate,
        TimeOnly newDepartureTime,
        TimeOnly newArrivalTime,
        string newCabinCode,
        string newETicketNumber)
    {
        InventoryId   = newInventoryId;
        TicketId      = newTicketId;
        FlightNumber  = newFlightNumber;
        Origin        = newOrigin;
        Destination   = newDestination;
        DepartureDate = newDepartureDate;
        DepartureTime = newDepartureTime;
        ArrivalTime   = newArrivalTime;
        CabinCode     = newCabinCode;
        ETicketNumber = newETicketNumber;
        SeatNumber    = string.Empty;
        UpdatedAt     = DateTime.UtcNow;
        Version++;
    }

    public static Manifest Create(
        Guid ticketId,
        Guid orderId,
        Guid inventoryId,
        string flightNumber,
        string origin,
        string destination,
        DateOnly departureDate,
        string aircraftType,
        string seatNumber,
        string cabinCode,
        string bookingReference,
        string eTicketNumber,
        string passengerId,
        string givenName,
        string surname,
        TimeOnly departureTime,
        TimeOnly arrivalTime,
        string bookingType,
        string? ssrCodes = null)
    {
        var now = DateTime.UtcNow;
        return new Manifest
        {
            ManifestId       = Guid.NewGuid(),
            TicketId         = ticketId,
            OrderId          = orderId,
            InventoryId      = inventoryId,
            FlightNumber     = flightNumber,
            Origin           = origin,
            Destination      = destination,
            DepartureDate    = departureDate,
            AircraftType     = aircraftType,
            SeatNumber       = seatNumber,
            CabinCode        = cabinCode,
            BookingReference = bookingReference,
            ETicketNumber    = eTicketNumber,
            PassengerId      = passengerId,
            GivenName        = givenName,
            Surname          = surname,
            SsrCodes         = ssrCodes,
            BookingType      = bookingType,
            DepartureTime    = departureTime,
            ArrivalTime      = arrivalTime,
            CheckedIn        = false,
            CheckedInAt      = null,
            CreatedAt        = now,
            UpdatedAt        = now,
            Version          = 1
        };
    }
}

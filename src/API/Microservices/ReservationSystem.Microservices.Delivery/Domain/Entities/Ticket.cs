namespace ReservationSystem.Microservices.Delivery.Domain.Entities;

/// <summary>
/// Core domain entity representing an electronic ticket issued for one passenger
/// on one flight segment. Maps to [delivery].[Ticket].
/// </summary>
public sealed class Ticket
{
    public Guid TicketId { get; private set; }
    public string ETicketNumber { get; private set; } = string.Empty;
    public Guid InventoryId { get; private set; }
    public string FlightNumber { get; private set; } = string.Empty;
    public DateTime DepartureDate { get; private set; }
    public string BookingReference { get; private set; } = string.Empty;
    public string PassengerId { get; private set; } = string.Empty;
    public string GivenName { get; private set; } = string.Empty;
    public string Surname { get; private set; } = string.Empty;
    public string CabinCode { get; private set; } = string.Empty;
    public string FareBasisCode { get; private set; } = string.Empty;
    public bool IsVoided { get; private set; }
    public DateTime? VoidedAt { get; private set; }
    public string TicketData { get; private set; } = "{}";
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public int Version { get; private set; }

    private Ticket() { }

    public static Ticket Create(
        string eTicketNumber,
        Guid inventoryId,
        string flightNumber,
        DateTime departureDate,
        string bookingReference,
        string passengerId,
        string givenName,
        string surname,
        string cabinCode,
        string fareBasisCode,
        string ticketData = "{}")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eTicketNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(bookingReference);

        var now = DateTime.UtcNow;
        return new Ticket
        {
            TicketId = Guid.NewGuid(),
            ETicketNumber = eTicketNumber,
            InventoryId = inventoryId,
            FlightNumber = flightNumber,
            DepartureDate = departureDate,
            BookingReference = bookingReference,
            PassengerId = passengerId,
            GivenName = givenName,
            Surname = surname,
            CabinCode = cabinCode,
            FareBasisCode = fareBasisCode,
            IsVoided = false,
            VoidedAt = null,
            TicketData = ticketData,
            CreatedAt = now,
            UpdatedAt = now,
            Version = 1
        };
    }

    public static Ticket Reconstitute(
        Guid ticketId, string eTicketNumber, Guid inventoryId,
        string flightNumber, DateTime departureDate, string bookingReference,
        string passengerId, string givenName, string surname,
        string cabinCode, string fareBasisCode, bool isVoided,
        DateTime? voidedAt, string ticketData,
        DateTime createdAt, DateTime updatedAt, int version)
    {
        return new Ticket
        {
            TicketId = ticketId,
            ETicketNumber = eTicketNumber,
            InventoryId = inventoryId,
            FlightNumber = flightNumber,
            DepartureDate = departureDate,
            BookingReference = bookingReference,
            PassengerId = passengerId,
            GivenName = givenName,
            Surname = surname,
            CabinCode = cabinCode,
            FareBasisCode = fareBasisCode,
            IsVoided = isVoided,
            VoidedAt = voidedAt,
            TicketData = ticketData,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            Version = version
        };
    }

    public void Void()
    {
        IsVoided = true;
        VoidedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }
}

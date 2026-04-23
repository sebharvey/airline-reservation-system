using System.Text.Json.Serialization;

namespace ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices.Dto;

public sealed class ManifestResponse
{
    [JsonPropertyName("entries")]
    public IReadOnlyList<ManifestEntryDto> Entries { get; init; } = [];
}

public sealed class ManifestEntryDto
{
    [JsonPropertyName("orderId")]
    public Guid OrderId { get; init; }

    [JsonPropertyName("bookingReference")]
    public string BookingReference { get; init; } = string.Empty;

    [JsonPropertyName("passengerId")]
    public string PassengerId { get; init; } = string.Empty;

    [JsonPropertyName("givenName")]
    public string GivenName { get; init; } = string.Empty;

    [JsonPropertyName("surname")]
    public string Surname { get; init; } = string.Empty;

    [JsonPropertyName("eTicketNumber")]
    public string ETicketNumber { get; init; } = string.Empty;

    [JsonPropertyName("seatNumber")]
    public string? SeatNumber { get; init; }

    [JsonPropertyName("cabinCode")]
    public string CabinCode { get; init; } = string.Empty;

    [JsonPropertyName("seatPosition")]
    public string? SeatPosition { get; init; } // "Window" | "Aisle" | "Middle"
}

public sealed class RebookManifestRequest
{
    [JsonPropertyName("toInventoryId")]
    public Guid ToInventoryId { get; init; }

    [JsonPropertyName("toFlightNumber")]
    public string ToFlightNumber { get; init; } = string.Empty;

    [JsonPropertyName("toOrigin")]
    public string ToOrigin { get; init; } = string.Empty;

    [JsonPropertyName("toDestination")]
    public string ToDestination { get; init; } = string.Empty;

    [JsonPropertyName("toDepartureDate")]
    public string ToDepartureDate { get; init; } = string.Empty;

    [JsonPropertyName("toDepartureTime")]
    public string ToDepartureTime { get; init; } = string.Empty;

    [JsonPropertyName("toArrivalTime")]
    public string ToArrivalTime { get; init; } = string.Empty;

    [JsonPropertyName("toCabinCode")]
    public string ToCabinCode { get; init; } = string.Empty;

    [JsonPropertyName("passengers")]
    public IReadOnlyList<RebookManifestPassengerDto> Passengers { get; init; } = [];
}

public sealed class RebookManifestPassengerDto
{
    [JsonPropertyName("passengerId")]
    public string PassengerId { get; init; } = string.Empty;

    [JsonPropertyName("eTicketNumber")]
    public string ETicketNumber { get; init; } = string.Empty;
}

public sealed class BookingTicketDto
{
    [JsonPropertyName("eTicketNumber")]
    public string ETicketNumber { get; init; } = string.Empty;

    [JsonPropertyName("isVoided")]
    public bool IsVoided { get; init; }
}

public sealed class ReissueTicketsRequest
{
    [JsonPropertyName("bookingReference")]
    public string BookingReference { get; init; } = string.Empty;

    [JsonPropertyName("voidedETicketNumbers")]
    public IReadOnlyList<string> CancelledETicketNumbers { get; init; } = [];

    [JsonPropertyName("passengers")]
    public IReadOnlyList<ReissuePassengerDto> Passengers { get; init; } = [];

    [JsonPropertyName("segments")]
    public IReadOnlyList<ReissueSegmentDto> Segments { get; init; } = [];

    [JsonPropertyName("reason")]
    public string Reason { get; init; } = string.Empty;

    [JsonPropertyName("actor")]
    public string Actor { get; init; } = string.Empty;
}

public sealed class ReissuePassengerDto
{
    [JsonPropertyName("passengerId")]
    public string PassengerId { get; init; } = string.Empty;

    [JsonPropertyName("givenName")]
    public string GivenName { get; init; } = string.Empty;

    [JsonPropertyName("surname")]
    public string Surname { get; init; } = string.Empty;

    [JsonPropertyName("passengerTypeCode")]
    public string? PassengerTypeCode { get; init; }
}

public sealed class ReissueSegmentDto
{
    [JsonPropertyName("inventoryId")]
    public Guid InventoryId { get; init; }

    [JsonPropertyName("flightNumber")]
    public string FlightNumber { get; init; } = string.Empty;

    [JsonPropertyName("departureDate")]
    public string DepartureDate { get; init; } = string.Empty;

    [JsonPropertyName("departureTime")]
    public string? DepartureTime { get; init; }

    [JsonPropertyName("origin")]
    public string Origin { get; init; } = string.Empty;

    [JsonPropertyName("destination")]
    public string Destination { get; init; } = string.Empty;

    [JsonPropertyName("cabinCode")]
    public string CabinCode { get; init; } = string.Empty;

    [JsonPropertyName("fareBasisCode")]
    public string FareBasisCode { get; init; } = string.Empty;
}

public sealed class ReissueTicketsResponse
{
    [JsonPropertyName("tickets")]
    public IReadOnlyList<ReissuedTicketDto> Tickets { get; init; } = [];
}

public sealed class ReissuedTicketDto
{
    [JsonPropertyName("eTicketNumber")]
    public string ETicketNumber { get; init; } = string.Empty;

    [JsonPropertyName("passengerId")]
    public string PassengerId { get; init; } = string.Empty;
}

public sealed class WriteManifestRequest
{
    [JsonPropertyName("bookingReference")]
    public string BookingReference { get; init; } = string.Empty;

    [JsonPropertyName("orderId")]
    public Guid OrderId { get; init; }

    [JsonPropertyName("inventoryId")]
    public Guid InventoryId { get; init; }

    [JsonPropertyName("flightNumber")]
    public string FlightNumber { get; init; } = string.Empty;

    [JsonPropertyName("departureDate")]
    public string DepartureDate { get; init; } = string.Empty;

    [JsonPropertyName("entries")]
    public IReadOnlyList<WriteManifestEntryDto> Entries { get; init; } = [];
}

public sealed class WriteManifestEntryDto
{
    [JsonPropertyName("passengerId")]
    public string PassengerId { get; init; } = string.Empty;

    [JsonPropertyName("givenName")]
    public string GivenName { get; init; } = string.Empty;

    [JsonPropertyName("surname")]
    public string Surname { get; init; } = string.Empty;

    [JsonPropertyName("eTicketNumber")]
    public string ETicketNumber { get; init; } = string.Empty;

    [JsonPropertyName("seatNumber")]
    public string? SeatNumber { get; init; }

    [JsonPropertyName("cabinCode")]
    public string CabinCode { get; init; } = string.Empty;

    [JsonPropertyName("seatPosition")]
    public string? SeatPosition { get; init; }
}

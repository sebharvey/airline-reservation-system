using System.Text.Json.Serialization;

namespace ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices.Dto;

public sealed class OfferSearchRequest
{
    [JsonPropertyName("origin")]
    public string Origin { get; init; } = string.Empty;

    [JsonPropertyName("destination")]
    public string Destination { get; init; } = string.Empty;

    [JsonPropertyName("departureDate")]
    public string Date { get; init; } = string.Empty;

    [JsonPropertyName("paxCount")]
    public int PaxCount { get; init; }

    [JsonPropertyName("includePrivateFares")]
    public bool IncludePrivateFares { get; init; } = false;
}

public sealed class OfferSearchResponse
{
    [JsonPropertyName("sessionId")]
    public string? SessionId { get; init; }

    [JsonPropertyName("flights")]
    public IReadOnlyList<OfferFlightDto> Flights { get; init; } = [];
}

public sealed class OfferFlightDto
{
    [JsonPropertyName("inventoryId")]
    public Guid InventoryId { get; init; }

    [JsonPropertyName("flightNumber")]
    public string FlightNumber { get; init; } = string.Empty;

    [JsonPropertyName("departureDate")]
    public string DepartureDate { get; init; } = string.Empty;

    [JsonPropertyName("departureTime")]
    public string DepartureTime { get; init; } = string.Empty;

    [JsonPropertyName("arrivalTime")]
    public string ArrivalTime { get; init; } = string.Empty;

    [JsonPropertyName("arrivalDayOffset")]
    public int ArrivalDayOffset { get; init; }

    [JsonPropertyName("origin")]
    public string Origin { get; init; } = string.Empty;

    [JsonPropertyName("destination")]
    public string Destination { get; init; } = string.Empty;

    [JsonPropertyName("offers")]
    public IReadOnlyList<OfferItemDto> Offers { get; init; } = [];
}

public sealed class OfferItemDto
{
    [JsonPropertyName("offerId")]
    public string OfferId { get; init; } = string.Empty;

    [JsonPropertyName("cabinCode")]
    public string CabinCode { get; init; } = string.Empty;

    [JsonPropertyName("seatsAvailable")]
    public int SeatsAvailable { get; init; }

    [JsonPropertyName("pointsPrice")]
    public int? PointsPrice { get; init; }
}

public sealed class FlightAvailabilityResponse
{
    [JsonPropertyName("flights")]
    public IReadOnlyList<AvailableFlightDto> Flights { get; init; } = [];
}

public sealed class AvailableFlightDto
{
    [JsonPropertyName("inventoryId")]
    public Guid InventoryId { get; init; }

    [JsonPropertyName("flightNumber")]
    public string FlightNumber { get; init; } = string.Empty;

    [JsonPropertyName("departureDate")]
    public string DepartureDate { get; init; } = string.Empty;

    [JsonPropertyName("departureTime")]
    public string DepartureTime { get; init; } = string.Empty;

    [JsonPropertyName("arrivalTime")]
    public string ArrivalTime { get; init; } = string.Empty;

    [JsonPropertyName("arrivalDayOffset")]
    public int ArrivalDayOffset { get; init; }

    [JsonPropertyName("origin")]
    public string Origin { get; init; } = string.Empty;

    [JsonPropertyName("destination")]
    public string Destination { get; init; } = string.Empty;

    [JsonPropertyName("cabins")]
    public IReadOnlyList<AvailableCabinDto> Cabins { get; init; } = [];
}

public sealed class AvailableCabinDto
{
    [JsonPropertyName("cabinCode")]
    public string CabinCode { get; init; } = string.Empty;

    [JsonPropertyName("seatsAvailable")]
    public int SeatsAvailable { get; init; }
}

public sealed class HoldInventoryRequest
{
    [JsonPropertyName("inventoryId")]
    public Guid InventoryId { get; init; }

    [JsonPropertyName("cabinCode")]
    public string CabinCode { get; init; } = string.Empty;

    [JsonPropertyName("seats")]
    public int Seats { get; init; }

    [JsonPropertyName("bookingReference")]
    public string BookingReference { get; init; } = string.Empty;
}

public sealed class ReleaseInventoryRequest
{
    [JsonPropertyName("inventoryId")]
    public Guid InventoryId { get; init; }

    [JsonPropertyName("cabinCode")]
    public string CabinCode { get; init; } = string.Empty;

    [JsonPropertyName("seats")]
    public int Seats { get; init; }

    [JsonPropertyName("releaseType")]
    public string ReleaseType { get; init; } = "Sold";
}

using System.Text.Json;
using ReservationSystem.Microservices.Ancillary.Application.Seat.CreateAircraftType;
using ReservationSystem.Microservices.Ancillary.Application.Seat.UpdateAircraftType;
using ReservationSystem.Microservices.Ancillary.Application.Seat.CreateSeatmap;
using ReservationSystem.Microservices.Ancillary.Application.Seat.UpdateSeatmap;
using ReservationSystem.Microservices.Ancillary.Application.Seat.CreateSeatPricing;
using ReservationSystem.Microservices.Ancillary.Application.Seat.UpdateSeatPricing;
using ReservationSystem.Microservices.Ancillary.Domain.Entities.Seat;
using ReservationSystem.Microservices.Ancillary.Models.Seat.Requests;
using ReservationSystem.Microservices.Ancillary.Models.Seat.Responses;

namespace ReservationSystem.Microservices.Ancillary.Models.Seat.Mappers;

/// <summary>
/// Static mapping methods between all model representations in the Seat domain.
///
/// Mapping directions:
///   HTTP request  →  Application command/query
///   Domain entity  →  HTTP response
/// </summary>
public static class SeatMapper
{
    // -------------------------------------------------------------------------
    // HTTP request → Application command
    // -------------------------------------------------------------------------

    public static CreateAircraftTypeCommand ToCommand(CreateAircraftTypeRequest request) =>
        new(
            AircraftTypeCode: request.AircraftTypeCode,
            Manufacturer: request.Manufacturer,
            TotalSeats: request.TotalSeats,
            FriendlyName: request.FriendlyName,
            CabinCounts: SerializeCabinCounts(request.CabinCounts));

    public static UpdateAircraftTypeCommand ToCommand(string aircraftTypeCode, UpdateAircraftTypeRequest request) =>
        new(
            AircraftTypeCode: aircraftTypeCode,
            Manufacturer: request.Manufacturer,
            TotalSeats: request.TotalSeats,
            FriendlyName: request.FriendlyName,
            CabinCounts: SerializeCabinCounts(request.CabinCounts),
            IsActive: request.IsActive);

    public static CreateSeatmapCommand ToCommand(CreateSeatmapRequest request) =>
        new(
            AircraftTypeCode: request.AircraftTypeCode,
            CabinLayout: request.CabinLayout.GetRawText());

    public static UpdateSeatmapCommand ToCommand(Guid seatmapId, UpdateSeatmapRequest request) =>
        new(
            SeatmapId: seatmapId,
            CabinLayout: request.CabinLayout?.GetRawText(),
            IsActive: request.IsActive);

    public static CreateSeatPricingCommand ToCommand(CreateSeatPricingRequest request) =>
        new(
            CabinCode: request.CabinCode,
            SeatPosition: request.SeatPosition,
            CurrencyCode: request.CurrencyCode,
            Price: request.Price,
            ValidFrom: request.ValidFrom,
            ValidTo: request.ValidTo);

    public static UpdateSeatPricingCommand ToCommand(Guid seatPricingId, UpdateSeatPricingRequest request) =>
        new(
            SeatPricingId: seatPricingId,
            CabinCode: request.CabinCode,
            SeatPosition: request.SeatPosition,
            CurrencyCode: request.CurrencyCode,
            Price: request.Price,
            IsActive: request.IsActive,
            ValidFrom: request.ValidFrom,
            ValidTo: request.ValidTo);

    // -------------------------------------------------------------------------
    // Domain entity → HTTP response
    // -------------------------------------------------------------------------

    public static AircraftTypeResponse ToResponse(AircraftType aircraftType) =>
        new()
        {
            AircraftTypeCode = aircraftType.AircraftTypeCode,
            Manufacturer = aircraftType.Manufacturer,
            FriendlyName = aircraftType.FriendlyName,
            TotalSeats = aircraftType.TotalSeats,
            CabinCounts = DeserializeCabinCounts(aircraftType.CabinCounts),
            IsActive = aircraftType.IsActive,
            CreatedAt = aircraftType.CreatedAt,
            UpdatedAt = aircraftType.UpdatedAt
        };

    public static IReadOnlyList<AircraftTypeResponse> ToResponse(IEnumerable<AircraftType> aircraftTypes) =>
        aircraftTypes.Select(ToResponse).ToList().AsReadOnly();

    public static SeatmapResponse ToResponse(Seatmap seatmap) =>
        new()
        {
            SeatmapId = seatmap.SeatmapId,
            AircraftType = seatmap.AircraftTypeCode,
            Version = seatmap.Version,
            IsActive = seatmap.IsActive,
            TotalSeats = 0, // Resolved from AircraftType at query time
            CabinLayout = seatmap.CabinLayout,
            CreatedAt = seatmap.CreatedAt,
            UpdatedAt = seatmap.UpdatedAt
        };

    public static SeatmapListItemResponse ToListItemResponse(Seatmap seatmap) =>
        new()
        {
            SeatmapId = seatmap.SeatmapId,
            AircraftTypeCode = seatmap.AircraftTypeCode,
            Version = seatmap.Version,
            IsActive = seatmap.IsActive,
            TotalSeats = 0, // Resolved from AircraftType at query time
            CreatedAt = seatmap.CreatedAt,
            UpdatedAt = seatmap.UpdatedAt
        };

    public static IReadOnlyList<SeatmapListItemResponse> ToListItemResponse(IEnumerable<Seatmap> seatmaps) =>
        seatmaps.Select(ToListItemResponse).ToList().AsReadOnly();

    public static SeatPricingResponse ToResponse(SeatPricing seatPricing) =>
        new()
        {
            SeatPricingId = seatPricing.SeatPricingId,
            CabinCode = seatPricing.CabinCode,
            SeatPosition = seatPricing.SeatPosition,
            CurrencyCode = seatPricing.CurrencyCode,
            Price = seatPricing.Price,
            Tax = seatPricing.Tax,
            IsActive = seatPricing.IsActive,
            ValidFrom = seatPricing.ValidFrom,
            ValidTo = seatPricing.ValidTo,
            CreatedAt = seatPricing.CreatedAt,
            UpdatedAt = seatPricing.UpdatedAt
        };

    public static IReadOnlyList<SeatPricingResponse> ToResponse(IEnumerable<SeatPricing> seatPricings) =>
        seatPricings.Select(ToResponse).ToList().AsReadOnly();

    private static readonly JsonSerializerOptions CabinCountJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private static string? SerializeCabinCounts(List<CabinCount>? cabinCounts) =>
        cabinCounts is null ? null : JsonSerializer.Serialize(cabinCounts, CabinCountJsonOptions);

    private static List<CabinCount>? DeserializeCabinCounts(string? json) =>
        string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<List<CabinCount>>(json, CabinCountJsonOptions);
}

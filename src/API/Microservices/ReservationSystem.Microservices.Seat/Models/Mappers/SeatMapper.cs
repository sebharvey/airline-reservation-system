using ReservationSystem.Microservices.Seat.Application.CreateAircraftType;
using ReservationSystem.Microservices.Seat.Application.UpdateAircraftType;
using ReservationSystem.Microservices.Seat.Application.CreateSeatmap;
using ReservationSystem.Microservices.Seat.Application.UpdateSeatmap;
using ReservationSystem.Microservices.Seat.Application.CreateSeatPricing;
using ReservationSystem.Microservices.Seat.Application.UpdateSeatPricing;
using ReservationSystem.Microservices.Seat.Models.Requests;
using ReservationSystem.Microservices.Seat.Models.Responses;

namespace ReservationSystem.Microservices.Seat.Models.Mappers;

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
            FriendlyName: request.FriendlyName);

    public static UpdateAircraftTypeCommand ToCommand(string aircraftTypeCode, UpdateAircraftTypeRequest request) =>
        new(
            AircraftTypeCode: aircraftTypeCode,
            Manufacturer: request.Manufacturer,
            TotalSeats: request.TotalSeats,
            FriendlyName: request.FriendlyName,
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
            Price: request.Price,
            IsActive: request.IsActive,
            ValidFrom: request.ValidFrom,
            ValidTo: request.ValidTo);

    // -------------------------------------------------------------------------
    // Domain entity → HTTP response
    // -------------------------------------------------------------------------

    public static AircraftTypeResponse ToResponse(Domain.Entities.AircraftType aircraftType) =>
        new()
        {
            AircraftTypeCode = aircraftType.AircraftTypeCode,
            Manufacturer = aircraftType.Manufacturer,
            FriendlyName = aircraftType.FriendlyName,
            TotalSeats = aircraftType.TotalSeats,
            IsActive = aircraftType.IsActive,
            CreatedAt = aircraftType.CreatedAt,
            UpdatedAt = aircraftType.UpdatedAt
        };

    public static IReadOnlyList<AircraftTypeResponse> ToResponse(IEnumerable<Domain.Entities.AircraftType> aircraftTypes) =>
        aircraftTypes.Select(ToResponse).ToList().AsReadOnly();

    public static SeatmapResponse ToResponse(Domain.Entities.Seatmap seatmap) =>
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

    public static SeatmapListItemResponse ToListItemResponse(Domain.Entities.Seatmap seatmap) =>
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

    public static IReadOnlyList<SeatmapListItemResponse> ToListItemResponse(IEnumerable<Domain.Entities.Seatmap> seatmaps) =>
        seatmaps.Select(ToListItemResponse).ToList().AsReadOnly();

    public static SeatPricingResponse ToResponse(Domain.Entities.SeatPricing seatPricing) =>
        new()
        {
            SeatPricingId = seatPricing.SeatPricingId,
            CabinCode = seatPricing.CabinCode,
            SeatPosition = seatPricing.SeatPosition,
            CurrencyCode = seatPricing.CurrencyCode,
            Price = seatPricing.Price,
            IsActive = seatPricing.IsActive,
            ValidFrom = seatPricing.ValidFrom,
            ValidTo = seatPricing.ValidTo,
            CreatedAt = seatPricing.CreatedAt,
            UpdatedAt = seatPricing.UpdatedAt
        };

    public static IReadOnlyList<SeatPricingResponse> ToResponse(IEnumerable<Domain.Entities.SeatPricing> seatPricings) =>
        seatPricings.Select(ToResponse).ToList().AsReadOnly();
}
